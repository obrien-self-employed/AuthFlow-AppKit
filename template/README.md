# AuthFlow.Template

`dotnet new` template pack that scaffolds a Blazor Server app pre-wired with
[AuthFlow.AppKit](https://www.nuget.org/packages/AuthFlow.AppKit) (license
activation + in-app update checking), Kestrel + Windows Service hosting, an
Inno Setup installer, and a generic `release.yml` CI/CD pipeline.

## Usage

```bash
dotnet new install AuthFlow.Template
dotnet new authflow-blazor -n MyApp
```

The generated project already references `AuthFlow.AppKit` (and transitively
`AuthFlow.LicenseClient`) - both public on nuget.org, so `dotnet restore`
works out of the box with no extra NuGet source or credentials.

Also discoverable from Visual Studio's **Create a new project** dialog
(search "authflow") once nuget.org has finished indexing the package.

See the full guide in the
[repository README](https://github.com/Brackford-0brien/AuthFlow-AppKit#readme).

## License

MIT
