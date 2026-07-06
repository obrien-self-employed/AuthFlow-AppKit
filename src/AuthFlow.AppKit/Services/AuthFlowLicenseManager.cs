using AuthFlow.LicenseClient.Http;
using AuthFlow.LicenseClient.Models;
using AuthFlow.LicenseClient.Storage;
using AuthFlow.LicenseClient.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuthFlow.AppKit.Services;

/// <summary>
/// Shared AuthFlow activate+check logic, used both by
/// <see cref="AuthFlowLicenseHostedService"/> at app startup/periodic
/// re-validation and by the license-activation import page when a customer
/// uploads a new ".lic" file at runtime.
///
/// Centralizing this here (rather than duplicating it in the page) means the
/// import flow gets exactly the same hybrid online/offline signature-check
/// behavior as the startup flow, and <see cref="AuthFlowLicenseState"/> is
/// always the single source of truth for the UI banner + any blocking gate
/// the consuming app implements.
/// </summary>
public sealed class AuthFlowLicenseManager
{
    private readonly IServiceProvider _rootServices;
    private readonly AuthFlowLicenseFileStore _fileStore;
    private readonly ILogger<AuthFlowLicenseManager> _logger;

    public AuthFlowLicenseManager(
        IServiceProvider rootServices,
        AuthFlowLicenseFileStore fileStore,
        ILogger<AuthFlowLicenseManager> logger)
    {
        _rootServices = rootServices;
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the license token to use: the imported ".lic" file takes
    /// priority (this is the real, per-customer activation path), with a
    /// fallback to <see cref="AuthFlowLicenseConfig.LicenseKey"/> (appsettings
    /// / user-secrets / environment variable) purely for local dev/testing
    /// convenience - see README.md. In production, customers are expected to
    /// always go through the file import flow.
    /// </summary>
    public string? ResolveLicenseKey() => _fileStore.Read() ?? AuthFlowLicenseConfig.LicenseKey;

    /// <summary>
    /// Runs <c>POST /api/activate</c> (idempotent, best-effort) followed by
    /// <see cref="ILicenseValidator.CheckAsync"/>, and publishes the result to
    /// <see cref="AuthFlowLicenseState"/>. Used for both the initial startup
    /// check and periodic re-validation.
    /// </summary>
    public async Task<LicenseCheckResult?> ActivateAndCheckAsync(string licenseKey, CancellationToken cancellationToken)
    {
        using var scope = _rootServices.CreateScope();
        var services = scope.ServiceProvider;

        var apiClient = services.GetRequiredService<IAuthFlowApiClient>();
        var fingerprintProvider = services.GetRequiredService<IDeviceFingerprintProvider>();
        var deviceFingerprint = fingerprintProvider.GetFingerprint();

        try
        {
            var activateResponse = await apiClient.ActivateAsync(new ActivateRequest
            {
                LicenseKey = licenseKey,
                DeviceFingerprint = deviceFingerprint,
                DeviceName = Environment.MachineName,
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AuthFlow: activation OK (status={Status}, activationId={ActivationId}, licenseStatus={LicenseStatus}, device={Device})",
                activateResponse.Status, activateResponse.Activation.Id, activateResponse.License.Status, deviceFingerprint);
        }
        catch (AuthFlowApiException ex)
        {
            // Not fatal: CheckAsync below may still succeed if this
            // install/license was already activated in a previous run, or
            // may correctly fail if the license is genuinely
            // invalid/revoked/exhausted - either way we let CheckAsync be the
            // source of truth for validity.
            _logger.LogWarning(ex, "AuthFlow: /api/activate rejected (status={StatusCode}): {ServerError}", ex.StatusCode, ex.ServerError);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning(ex, "AuthFlow: /api/activate unreachable - continuing, CheckAsync will fall back to offline mode if needed.");
        }

        var validator = services.GetRequiredService<ILicenseValidator>();
        try
        {
            var result = await validator.CheckAsync(licenseKey, cancellationToken).ConfigureAwait(false);
            AuthFlowLicenseState.Update(result);

            if (result.IsValid)
            {
                _logger.LogInformation(
                    "AuthFlow: license check OK (offline={IsOffline}, licenseId={LicenseId}, status={Status}, expiresAt={ExpiresAt})",
                    result.IsOffline, result.Payload?.LicenseId, result.Payload?.Status, result.Payload?.ExpiresAt);
            }
            else
            {
                _logger.LogWarning("AuthFlow: license check FAILED - {Reason}", result.Reason);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthFlow: unexpected error while checking license.");
            AuthFlowLicenseState.UpdateFailure($"Erreur inattendue: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Called from the license-activation upload page: activates and checks
    /// the newly provided token, and - only if it turns out valid - persists
    /// it via <see cref="AuthFlowLicenseFileStore"/> so it survives future
    /// app restarts without re-uploading. A token that fails validation is
    /// deliberately NOT persisted, so a bad upload can't get "stuck" as the
    /// active license file.
    /// </summary>
    public async Task<(bool Success, string? Reason)> ImportAndActivateAsync(string rawFileContent, CancellationToken cancellationToken)
    {
        var licenseKey = rawFileContent.Trim();
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return (false, "Le fichier de licence est vide.");
        }

        var result = await ActivateAndCheckAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return (false, "Erreur inattendue pendant la vérification de la licence.");
        }

        if (!result.IsValid)
        {
            return (false, result.Reason ?? "Licence invalide.");
        }

        _fileStore.Save(licenseKey);
        _logger.LogInformation("AuthFlow: new license imported and persisted to {Path}.", _fileStore.FilePath);
        return (true, null);
    }
}
