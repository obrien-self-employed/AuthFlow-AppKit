using AuthFlow.LicenseClient.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthFlow.AppKit.Services;

/// <summary>
/// Bootstraps the AuthFlow.LicenseClient SDK for ASP.NET Core / Blazor
/// Server apps. The SDK's own <c>LicenseHeartbeatService</c> (registered by
/// <c>AddAuthFlowLicense</c>) is started/stopped automatically by the host
/// like any other <see cref="IHostedService"/> - no manual wiring needed.
///
/// This service only handles what the SDK does NOT do automatically:
///  1. Resolve the license token to use, via <see cref="AuthFlowLicenseManager.ResolveLicenseKey"/>
///     - the imported ".lic" file (see <see cref="AuthFlowLicenseFileStore"/>)
///     takes priority; falls back to config (appsettings/user-secrets/env
///     var) only for local dev/testing convenience.
///  2. <c>POST /api/activate</c> once for this machine/install (idempotent
///     server-side) + <see cref="AuthFlow.LicenseClient.Validation.ILicenseValidator.CheckAsync"/>
///     - hybrid offline signature check + online validation - and publish
///     the result to <see cref="AuthFlowLicenseState"/> for the UI banner
///     and any blocking gate the consuming app implements.
///  3. Periodically repeat step 2 (same cadence as the heartbeat) so a
///     license revoked/expired while the app is already running is detected
///     without requiring a restart (the heartbeat itself only sends
///     usage/uptime telemetry - it does not carry license validity).
///
/// If no license has been imported yet (no ".lic" file and no config
/// fallback), <see cref="AuthFlowLicenseState"/> is marked invalid with an
/// explicit reason and no SDK network calls are made - the consuming app's UI
/// should then redirect to its license-activation page so the customer can
/// upload their license file. See README.md for the full import flow and the
/// mono-tenant deployment rationale.
/// </summary>
public sealed class AuthFlowLicenseHostedService : IHostedService, IDisposable
{
    private readonly AuthFlowLicenseManager _manager;
    private readonly ILogger<AuthFlowLicenseHostedService> _logger;
    private readonly AuthFlowLicenseOptions _options;
    private CancellationTokenSource? _revalidationCts;
    private Task? _revalidationLoop;

    public AuthFlowLicenseHostedService(
        AuthFlowLicenseManager manager,
        ILogger<AuthFlowLicenseHostedService> logger,
        IOptions<AuthFlowLicenseOptions> options)
    {
        _manager = manager;
        _logger = logger;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var licenseKey = _manager.ResolveLicenseKey();
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            _logger.LogWarning("AuthFlow: no license imported yet (no .lic file, no AuthFlow:LicenseKey fallback) - skipping activation/validation until one is uploaded.");
            AuthFlowLicenseState.UpdateFailure("Aucune licence importée");
        }
        else
        {
            await _manager.ActivateAndCheckAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        }

        // Note: we deliberately do NOT start LicenseHeartbeatService
        // ourselves here - it is registered as its own IHostedService by
        // AddAuthFlowLicense and the ASP.NET Core generic host already
        // starts/stops it automatically alongside this service.

        // Periodically re-run the check so a mid-session revocation/
        // expiration (or a fresh import while the app is running) is caught
        // without requiring an app restart.
        _revalidationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _revalidationLoop = Task.Run(() => PeriodicRevalidationLoopAsync(_options.HeartbeatInterval, _revalidationCts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _revalidationCts?.Cancel();
        if (_revalidationLoop is not null)
        {
            try
            {
                await _revalidationLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task PeriodicRevalidationLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromHours(1);
        }

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            // Re-resolve each tick: picks up a license imported via the
            // activation page after this host started, without waiting for a
            // full app restart.
            var licenseKey = _manager.ResolveLicenseKey();
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                continue;
            }

            try
            {
                await _manager.ActivateAndCheckAsync(licenseKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AuthFlow: periodic re-check threw an unexpected error - keeping previous state.");
            }
        }
    }

    public void Dispose()
    {
        _revalidationCts?.Dispose();
    }
}

/// <summary>
/// Holds a fallback license key read from configuration
/// (<c>AuthFlow:LicenseKey</c> in appsettings.json / user-secrets /
/// environment variable), used only when no ".lic" file has been imported
/// yet - see <see cref="AuthFlowLicenseManager.ResolveLicenseKey"/>. Kept
/// mainly for local dev/testing convenience so the app can boot without
/// going through the upload UI every time; production customers are
/// expected to always import their own ".lic" file.
/// </summary>
public static class AuthFlowLicenseConfig
{
    public static string? LicenseKey { get; set; }
}
