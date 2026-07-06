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
`Brackford-0brien/AuthFlow`, `sdk/dotnet/AuthFlow.LicenseClient`). That SDK
isn't built from source in this repo - the pre-built `.nupkg` is vendored
under `local-nuget-feed/` and re-published to this same GitHub Packages feed
by `publish-package.yml`, so consumers never need the vendored copy or the
`AuthFlow` source repo at all - just this repo's feed.

## Configure the GitHub Packages feed on your dev machine

GitHub Packages NuGet feeds require authentication even for read access on
public feeds owned by a user account (this is a GitHub limitation, not
something specific to this repo). You need a **Personal Access Token (PAT)**
with **exactly the `read:packages` scope** (classic PAT) - nothing else is
required to *consume* packages. (A fine-grained PAT with "Packages: Read"
works too, but classic + `read:packages` is simplest.)

**Step by step:**

1. Go to https://github.com/settings/tokens → "Generate new token (classic)".
2. Tick only `read:packages`. Give it an expiry you're comfortable with.
3. Generate it and copy the token value (starts with `ghp_...`).
4. Create a `nuget.config` **next to your `.sln`/project** (or globally at
   `%AppData%\NuGet\NuGet.Config` to make it available to every project on
   your machine) with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="authflow-appkit-github" value="https://nuget.pkg.github.com/Brackford-0brien/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <authflow-appkit-github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="ghp_YOUR_read_packages_TOKEN" />
    </authflow-appkit-github>
  </packageSourceCredentials>
</configuration>
```

Or equivalently via the CLI (writes the same thing):

```powershell
dotnet nuget add source https://nuget.pkg.github.com/Brackford-0brien/index.json `
  -n authflow-appkit-github -u YOUR_GITHUB_USERNAME -p ghp_YOUR_read_packages_TOKEN --store-password-in-clear-text
```

That's the **only** local setup needed. No repo clone, no local feed, no
extra steps - `dotnet new install` / `dotnet restore` do the rest.

> **Never commit a real token into `nuget.config`.** Keep it in your
> user-level `%AppData%\NuGet\NuGet.Config` instead of a repo-local file if
> you're worried about accidentally committing it, or add `nuget.config` to
> `.gitignore` in the project you're creating.

## Create a new app in 2 commands

```powershell
dotnet new install AuthFlow.Template::*
dotnet new authflow-blazor -n MyNewApp
cd MyNewApp
dotnet build
```

The `-n` value becomes the project/namespace/service name, the installer's
`AppId` GUID is regenerated automatically per instantiation. Adjust
`ProductDisplayName`/`ServiceBaseName`/branding strings directly in
`Program.cs`/`appsettings.json` after generation if they should differ from
the project name.

**The generated `.csproj` already references `AuthFlow.AppKit` - no manual
`dotnet add package` step required.** `AuthFlow.LicenseClient` (the SDK) is
resolved automatically too, as a transitive dependency of `AuthFlow.AppKit`
(you won't see it as a *top-level* reference in `dotnet list package` - that
package only lists direct references by default - but `dotnet restore` /
`dotnet build` pull it in without you doing anything). Verified for real with
`dotnet list package` on a freshly generated project:

```
Top-level Package                                Requested    Resolved
> AuthFlow.AppKit                                 0.1.0        0.1.0
> Microsoft.Extensions.Hosting.WindowsServices    10.0.0       10.0.0
> Radzen.Blazor                                   6.0.0        6.0.0
```

This generates a Blazor Server project with:
- `Program.cs` pre-wired with `builder.Services.AddAuthFlowAppKit(...)`, `builder.Host.UseWindowsService(...)`, Kestrel binding from config.
- `appsettings.json` with an `AuthFlow` section ready for your product's `ProductId`/`ServerUrl`/`PublicKeys`.
- `installer/setup.iss` (Inno Setup) with a freshly generated AppId GUID and your app/service name substituted in.
- `.github/workflows/release.yml` - the same generic, parameterized CI/CD pipeline validated on FneManuelInvoiceApp (build → Inno Setup → checksum → GitHub Release on a separate public releases-only repo). Just configure `RELEASES_REPO` / `RELEASES_REPO_TOKEN` for the new app's own releases repo (see FneManuelInvoiceApp's `INSTALLER.md` §7 for the exact steps - not duplicated here to avoid drift; copy that doc alongside the workflow when you scaffold a new app).

## Publishing a new package/template version

1. Bump `<Version>` in `src/AuthFlow.AppKit/AuthFlow.AppKit.csproj` (and
   `template/AuthFlow.Template.csproj` if you changed the template).
2. `git tag vX.Y.Z && git push origin vX.Y.Z`
3. `.github/workflows/publish-package.yml` builds, packs, and pushes
   `AuthFlow.AppKit`, `AuthFlow.Template`, **and** the vendored
   `AuthFlow.LicenseClient` SDK nupkg to
   `https://nuget.pkg.github.com/Brackford-0brien/index.json`, all using the
   default `GITHUB_TOKEN` (this repo's Actions permissions are set to
   `packages: write`, which is all GitHub Packages needs for a package owned
   by the same repo/owner - no extra PAT required for publishing).
4. You can also trigger a publish without a tag via **Actions → Publish
   AuthFlow.AppKit NuGet package → Run workflow** (`workflow_dispatch`) - useful
   to republish the vendored SDK nupkg alone if it changes without bumping
   AppKit's own version.

## Verifying a clean dev machine can actually consume the packages

This was tested for real, not assumed: `.github/workflows/test-clean-consume.yml`
(on-demand, `workflow_dispatch`) runs on a brand-new GitHub-hosted runner -
literally nothing preinstalled beyond the .NET SDK - and does exactly what a
new team member would do:

1. Writes a `nuget.config` with only `nuget.org` + this repo's GitHub Packages
   feed, authenticated with a PAT stored as the `TEST_READ_PACKAGES_PAT`
   secret (scope: `read:packages` only).
2. `dotnet new install AuthFlow.Template::0.1.0`
3. `dotnet new authflow-blazor -n TestApp`
4. `dotnet restore` + `dotnet list package` in the generated project
5. `dotnet build -c Release`

Result of the last real run: **all green**, `AuthFlow.AppKit 0.1.0` resolved
automatically as a top-level package (see the exact output in the "Create a
new app" section above), `AuthFlow.LicenseClient` resolved transitively, and
the generated app built with 0 warnings/0 errors - confirmed with no local
feed, no repo clone, no manual `dotnet add package`, nothing beyond the
documented `nuget.config` + a read-only PAT.

If you want to run this check yourself (e.g. after a package change), add a
`TEST_READ_PACKAGES_PAT` repository secret (classic PAT, `read:packages`
scope) once, then run the workflow from the Actions tab whenever you want to
re-verify.

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

## Real end-to-end test performed

Both packages were built, packed, and validated for real (not just compiled):

1. `dotnet pack` on `AuthFlow.AppKit.csproj` and `template/AuthFlow.Template.csproj` → both produced valid `.nupkg`s.
2. `dotnet new install AuthFlow.Template.0.1.0.nupkg` from a local feed → template registered successfully (`authflow-blazor`).
3. `dotnet new authflow-blazor -n TestApp` → generated a project with `TestApp.csproj`, a freshly generated installer AppId GUID substituted into `setup.iss`, and all `AuthFlowApp1` references renamed to `TestApp`.
4. `dotnet build` on the generated project → **0 warnings, 0 errors**.
5. `dotnet run` with a real `ProductId`/`LicenseKey`/`PublicKeys` from the FneManuelInvoiceApp pilot's AuthFlow product, against the real production server `https://auth-flow-dun.vercel.app`:
   - `POST /api/activate` and `POST /api/validate` were called for real (visible in logs), confirming the extracted `AuthFlowLicenseManager`/`AuthFlowLicenseHostedService` work unchanged once separated from the original app.
   - The (now-revoked, from earlier pilot testing) license correctly came back as `Revoked` - proving the SDK's Ed25519 signature/validation pipeline still functions end-to-end through the extracted code.
   - `GET /api/updates/check` was called for real from the generated app's `/update-check` page, confirming `AppUpdateService` works too.
   - `/`, `/license-activation`, and `/update-check` all returned HTTP 200 with the expected page titles once `AdditionalAssemblies`/`AddAdditionalAssemblies` were wired in `Routes.razor`/`Program.cs` so ASP.NET Core discovers `@page`-routed components living in the referenced `AuthFlow.AppKit` package assembly (a real, non-obvious integration issue caught only by this real run, not by `dotnet build` alone).
6. Test artifacts (temp `dotnet new install`, generated `TestApp` folder) were cleaned up afterward.

## Design notes / assumptions carried over from the pilot

- **Mono-tenant deployment assumption**: one server/process instance per
  customer, so the license file store is a single flat file, not a
  per-tenant database row.
- **Radzen.Blazor dependency**: the extracted Razor components use Radzen
  (`RadzenIcon`, `RadzenButton`, `RadzenAlert`, etc.) - this is an explicit
  package dependency of `AuthFlow.AppKit`. Confirmed with the client: most of
  their apps already use Radzen, so this stays a hard dependency (not made
  optional) - kept simple on purpose.
- **Public releases-only repo pattern recommended**: don't distribute a
  GitHub token to client machines just to download installers. Create a
  second, public, code-free repo per app (e.g. `MyNewApp-releases`) and
  point the release workflow's `RELEASES_REPO` variable at it - downloads
  are then fully anonymous. `AppUpdateService`'s GitHub-API authenticated
  path is a defensive fallback only, for the (discouraged) case of a private
  releases repo.
