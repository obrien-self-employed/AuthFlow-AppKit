using AuthFlow.LicenseClient.DependencyInjection;
using AuthFlow.LicenseClient.Options;
using AuthFlow.AppKit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthFlow.AppKit;

/// <summary>
/// One-call registration for the full AuthFlow AppKit stack: the underlying
/// AuthFlow.LicenseClient SDK (license validation + update checking +
/// heartbeat), the license file store/manager/hosted-service that drive the
/// license-activation flow, and <see cref="Services.AppUpdateService"/> for
/// the in-app "check for updates" screen.
///
/// Usage in <c>Program.cs</c> (see README.md "Intégration" for the full
/// appsettings.json shape this reads from):
/// <code>
/// builder.Services.AddAuthFlowAppKit(builder.Configuration, options =>
/// {
///     options.ProductDisplayName = "Radisson Connect";
///     options.ServiceBaseName = "RadissonConnect";
/// });
/// </code>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AuthFlow SDK (reading the "AuthFlow" configuration
    /// section for ProductId/ServerUrl/PublicKeys/GracePeriod/HeartbeatInterval),
    /// the AppKit's license file store/manager/hosted service, and
    /// <see cref="Services.AppUpdateService"/> (which reads the optional
    /// "GitHub:Token" configuration key - see README.md).
    /// </summary>
    public static IServiceCollection AddAuthFlowAppKit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthFlowAppKitOptions>? configureAppKit = null)
    {
        var appKitOptions = new AuthFlowAppKitOptions();
        configureAppKit?.Invoke(appKitOptions);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(appKitOptions));

        var authFlowSection = configuration.GetSection("AuthFlow");
        AuthFlowLicenseConfig.LicenseKey = authFlowSection["LicenseKey"];

        services.AddAuthFlowLicense(options =>
        {
            options.ProductId = authFlowSection["ProductId"] ?? string.Empty;
            options.ServerUrl = authFlowSection["ServerUrl"] ?? string.Empty;
            options.GracePeriod = TimeSpan.FromDays(authFlowSection.GetValue("GracePeriodDays", 7));
            options.HeartbeatInterval = TimeSpan.FromMinutes(authFlowSection.GetValue("HeartbeatIntervalMinutes", 60));
            options.HttpTimeout = TimeSpan.FromSeconds(authFlowSection.GetValue("HttpTimeoutSeconds", 20));

            var publicKeys = authFlowSection.GetSection("PublicKeys").GetChildren();
            foreach (var key in publicKeys)
            {
                if (!string.IsNullOrWhiteSpace(key.Value))
                {
                    options.PublicKeys[key.Key] = key.Value;
                }
            }
        });

        services.AddSingleton<AuthFlowLicenseFileStore>();
        services.AddSingleton<AuthFlowLicenseManager>();
        services.AddHostedService<AuthFlowLicenseHostedService>();

        services.AddHttpClient(nameof(Services.AppUpdateService));
        services.AddScoped<Services.AppUpdateService>();

        return services;
    }
}
