# AuthFlow.AppKit

Reusable AuthFlow license activation/validation + in-app update-check
integration for ASP.NET Core Blazor Server apps (Kestrel + Windows Service
hosting). Extracted from the FneManuelInvoiceApp pilot integration.

Wraps [`AuthFlow.LicenseClient`](https://www.nuget.org/packages/AuthFlow.LicenseClient)
with production-ready building blocks:

- `AuthFlowLicenseFileStore` / `AuthFlowLicenseManager` - `.lic` file import,
  activation against the AuthFlow server, local persistence.
- `AuthFlowLicenseHostedService` - periodic background revalidation with a
  `Changed` event other components can subscribe to.
- `AppUpdateService` - generic (non-app-specific) update-check client for
  AuthFlow's `/api/updates/check` endpoint.
- `AuthFlowBanner.razor` / `LicenseActivation.razor` / `UpdateCheck.razor` -
  ready-to-drop Blazor components for license status and update banners.

## Installation

```bash
dotnet add package AuthFlow.AppKit
```

This pulls in `AuthFlow.LicenseClient` transitively - both packages are
public on nuget.org, so no extra NuGet source or credentials are required.

## Quick start

See the full guide in the
[repository README](https://github.com/Brackford-0brien/AuthFlow-AppKit#readme),
or scaffold a ready-to-run app with the companion template:

```bash
dotnet new install AuthFlow.Template
dotnet new authflow-blazor -n MyApp
```

## License

MIT
