# AuthFlow AppKit

Reusable AuthFlow license-activation + in-app update-check integration for
ASP.NET Core Blazor Server apps (Kestrel + Windows Service), extracted from
the real pilot integration validated on `Brackford-0brien/FneManuelInvoiceApp`
(`dev/web-version`). Two NuGet packages, published on this repo's GitHub
Packages feed:

| Package | What it is |
|---|---|
| `AuthFlow.AppKit` | Class library: license file store/manager/hosted-service, `AppUpdateService`, and the Razor UI pieces (`AuthFlowBanner`, `LicenseActivation`, `UpdateCheck`). |
| `AuthFlow.Template` | A `dotnet new` template pack that scaffolds a brand-new Blazor Server app already wired to `AuthFlow.AppKit` + the AuthFlow SDK, Kestrel/Windows Service host, Inno Setup installer, and the generic release CI/CD pipeline. |

It depends on `AuthFlow.LicenseClient` (the AuthFlow SDK, from
`Brackford-0brien/AuthFlow`, `sdk/dotnet/AuthFlow.LicenseClient`) - not yet on
a real feed, so for now this repo vendors the same local `.nupkg` copy used
by FneManuelInvoiceApp (see `local-nuget-feed/`). Once the SDK is published
for real, swap the `NuGet.Config` source accordingly.

## Create a new app in 2 commands

```powershell
dotnet new install AuthFlow.Template::*
dotnet new authflow-blazor -n MyNewApp --product-display-name "My New App" --service-base-name MyNewApp
cd MyNewApp
dotnet build
```

This generates a Blazor Server project with:
- `Program.cs` pre-wired with `builder.Services.AddAuthFlowAppKit(...)`, `builder.Host.UseWindowsService(...)`, Kestrel binding from config.
- `appsettings.json` with an `AuthFlow` section ready for your product's `ProductId`/`ServerUrl`/`PublicKeys`.
- `installer/setup.iss` (Inno Setup) with a freshly generated AppId GUID and your app/service name substituted in.
- `.github/workflows/release.yml` - the same generic, parameterized CI/CD pipeline validated on FneManuelInvoiceApp (build → Inno Setup → checksum → GitHub Release on a separate public releases-only repo). Just configure `RELEASES_REPO` / `RELEASES_REPO_TOKEN` for the new app's own releases repo (see FneManuelInvoiceApp's `INSTALLER.md` §7 for the exact steps - not duplicated here to avoid drift; copy that doc alongside the workflow when you scaffold a new app).

## Configure the GitHub Packages feed on your dev machine

GitHub Packages NuGet feeds require authentication even for read access on
public feeds owned by a user account. Add a `nuget.config` (repo-local or
`%AppData%\NuGet\NuGet.Config`) with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="authflow-appkit-github" value="https://nuget.pkg.github.com/Brackford-0brien/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <authflow-appkit-github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PAT_WITH_read:packages_SCOPE" />
    </authflow-appkit-github>
  </packageSourceCredentials>
</configuration>
```

Or via CLI:

```powershell
dotnet nuget add source https://nuget.pkg.github.com/Brackford-0brien/index.json `
  -n authflow-appkit-github -u YOUR_GITHUB_USERNAME -p YOUR_PAT --store-password-in-clear-text
```

The PAT needs the `read:packages` scope (classic PAT), or a fine-grained PAT
with "Packages: Read" on this repo.

## Publishing a new package/template version

1. Bump `<Version>` in `src/AuthFlow.AppKit/AuthFlow.AppKit.csproj` (and
   `template/AuthFlow.Template.csproj` if you changed the template).
2. `git tag vX.Y.Z && git push origin vX.Y.Z`
3. `.github/workflows/publish-package.yml` builds, packs, and pushes both
   packages to `https://nuget.pkg.github.com/Brackford-0brien/index.json`
   using the default `GITHUB_TOKEN` (this repo's Actions permissions are set
   to `packages: write`, which is all GitHub Packages needs for a package
   owned by the same repo/owner - no extra PAT required for publishing).

## What's inside `AuthFlow.AppKit`

- `Services/AuthFlowLicenseFileStore.cs` - persists the imported `.lic` token under `App_Data/authflow-license.lic`.
- `Services/AuthFlowLicenseManager.cs` - shared activate+check logic (used at startup and by the import page).
- `Services/AuthFlowLicenseHostedService.cs` + `AuthFlowLicenseConfig` - runs the initial + periodic license check as an `IHostedService`. The SDK's own heartbeat service is registered separately by `AddAuthFlowLicense` (from `AuthFlow.LicenseClient`) and started automatically by the same host - no extra wiring needed.
- `Services/AuthFlowLicenseState.cs` - process-wide last-check-result snapshot + change notification, read by the Razor banner/pages.
- `Services/AppUpdateService.cs` - wraps the SDK's `IUpdateChecker`, adds download + SHA-256 checksum verification + installer launch, with an optional GitHub-API authenticated-download fallback for private release repos (not needed for the recommended public releases-repo pattern).
- `Components/AuthFlowBanner.razor` - non-blocking warning banner (invalid/offline license), drop into your `MainLayout`.
- `Components/LicenseActivation.razor` - `.lic` file upload/import page (`/license-activation`).
- `Components/UpdateCheck.razor` - "check for updates" page (`/update-check`), with download+install button.
- `AuthFlowAppKitOptions.cs` - `ProductDisplayName` / `CompanyName` / `ServiceBaseName`, the knobs used to de-hardcode app-specific strings (temp folder names, installer filename fallback, User-Agent, UI titles/footer) that were previously hardcoded to "FneManuelInvoiceApp" in the original pilot code.
- `ServiceCollectionExtensions.cs` - `services.AddAuthFlowAppKit(configuration, options => {...})`, the single call that wires everything above.

### `Program.cs` usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthFlowAppKit(builder.Configuration, options =>
{
    options.ProductDisplayName = "My New App";
    options.CompanyName = "Brackford-0brien";
    options.ServiceBaseName = "MyNewApp"; // used for temp folder / installer filename fallback
});

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = builder.Configuration["WindowsService:ServiceName"] ?? "MyNewApp";
});
```

### `appsettings.json` shape it reads

```json
{
  "AuthFlow": {
    "ProductId": "your-product-id",
    "ServerUrl": "https://auth-flow-dun.vercel.app",
    "LicenseKey": "", // dev/testing fallback only - production uses the .lic import flow
    "GracePeriodDays": 7,
    "HeartbeatIntervalMinutes": 60,
    "HttpTimeoutSeconds": 20,
    "PublicKeys": {
      "test-key": "MCowBQYDK2VwAyEA..."
    }
  },
  "GitHub": {
    "Token": "" // only needed if your installer releases repo is PRIVATE - not recommended, see below
  }
}
```

## Design notes / assumptions carried over from the pilot

- **Mono-tenant deployment assumption**: one server/process instance per
  customer, so the license file store is a single flat file, not a
  per-tenant database row.
- **Radzen.Blazor dependency**: the extracted Razor components use Radzen
  (`RadzenIcon`, `RadzenButton`, `RadzenAlert`, etc.) - this is an explicit
  package dependency of `AuthFlow.AppKit`. If a target app doesn't already
  use Radzen, it will be pulled in transitively. This was the pragmatic
  choice given the existing FneManuelInvoiceApp UI; flag it if a future app
  needs a Radzen-free UI variant.
- **Public releases-only repo pattern recommended**: don't distribute a
  GitHub token to client machines just to download installers. Create a
  second, public, code-free repo per app (e.g. `MyNewApp-releases`) and
  point the release workflow's `RELEASES_REPO` variable at it - downloads
  are then fully anonymous. `AppUpdateService`'s GitHub-API authenticated
  path is a defensive fallback only, for the (discouraged) case of a private
  releases repo.
