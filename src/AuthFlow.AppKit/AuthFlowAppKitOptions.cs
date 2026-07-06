namespace AuthFlow.AppKit;

/// <summary>
/// App-specific display/branding values used by the AppKit's Blazor
/// components and services, so this library never has to hardcode a
/// product name (unlike the original FneManuelInvoiceApp pilot, where these
/// were literal strings scattered across several files). Configure once via
/// <c>services.AddAuthFlowAppKit(options => { ... })</c> in the consuming
/// app's <c>Program.cs</c> - see README.md "Intégration" for the full setup.
/// </summary>
public sealed class AuthFlowAppKitOptions
{
    /// <summary>
    /// Human-friendly product name shown in the license-activation page
    /// header/subtitle and in Release titles built by the CI/CD template
    /// (e.g. "Radisson Connect"). Falls back to <see cref="ServiceBaseName"/>
    /// if not set.
    /// </summary>
    public string? ProductDisplayName { get; set; }

    /// <summary>
    /// Company/publisher name shown in the license-activation page footer
    /// (e.g. "© 2026 Brackford-0brien - Tous droits réservés").
    /// </summary>
    public string CompanyName { get; set; } = "Brackford-0brien";

    /// <summary>
    /// Base name used to build default temp-folder/installer-file-name
    /// fallbacks in <see cref="Services.AppUpdateService"/> (e.g.
    /// "RadissonConnect" -&gt; temp dir "RadissonConnect-Update", installer
    /// fallback name "RadissonConnect-Setup-{version}.exe"). This should
    /// match <c>vars.PROJECT_NAME</c> used by the release.yml CI/CD template
    /// so file names line up between what the app expects and what CI/CD
    /// actually publishes.
    /// </summary>
    public string ServiceBaseName { get; set; } = "App";
}
