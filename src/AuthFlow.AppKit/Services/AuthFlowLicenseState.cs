using AuthFlow.LicenseClient.Validation;

namespace AuthFlow.AppKit.Services;

/// <summary>
/// Process-wide, read-only snapshot of the last AuthFlow license check
/// result. Populated by <see cref="AuthFlowLicenseHostedService"/> at app
/// startup (and refreshed by later checks). UI (e.g. <c>AuthFlowBanner</c>
/// or a blocking gate in the consuming app's main layout) reads this to
/// warn/gate the user - see README.md for recommended patterns.
/// </summary>
public static class AuthFlowLicenseState
{
    public static bool HasRun { get; private set; }
    public static bool IsValid { get; private set; }
    public static bool IsOffline { get; private set; }
    public static string? Reason { get; private set; }
    public static DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Raised whenever <see cref="Update"/> or <see cref="UpdateFailure"/> is
    /// called. The license check runs on a background thread (hosted
    /// service), well after the Blazor UI has already rendered once, so
    /// components must subscribe to this event and call
    /// <c>StateHasChanged</c> themselves to reflect the result - see
    /// <c>AuthFlowBanner.razor</c>. Without this, a revoked/invalid license
    /// would silently fail to show any warning, since nothing would ever
    /// trigger a re-render after the initial (pre-check) render.
    /// </summary>
    public static event Action? Changed;

    public static void Update(LicenseCheckResult result)
    {
        HasRun = true;
        IsValid = result.IsValid;
        IsOffline = result.IsOffline;
        Reason = result.Reason;
        ExpiresAt = result.Payload?.ExpiresAt;
        Changed?.Invoke();
    }

    public static void UpdateFailure(string reason)
    {
        HasRun = true;
        IsValid = false;
        IsOffline = false;
        Reason = reason;
        ExpiresAt = null;
        Changed?.Invoke();
    }
}
