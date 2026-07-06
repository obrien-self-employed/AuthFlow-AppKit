namespace AuthFlow.AppKit.Services;

/// <summary>
/// Persists the raw AuthFlow license token (the content of the ".lic" file a
/// customer downloads from the AuthFlow dashboard and uploads via the
/// license-activation screen) on this server instance, so it survives app
/// restarts without needing to be re-uploaded every launch.
///
/// Consuming apps are expected to be deployed mono-tenant - one server/
/// process instance per customer. A single process-wide file is therefore
/// enough; there is no per-user/per-tenant storage requirement here, unlike
/// a true multi-tenant SaaS where the license would need to live in a
/// database keyed by TenantId instead of a flat file.
///
/// Stored under <c>App_Data</c> (IIS/Kestrel-friendly, writable-on-deploy
/// convention), as plain text (the AuthFlow token is itself a signed,
/// tamper-evident JWT-like structure - it does not need additional
/// encryption at rest to be safe; see README.md).
/// </summary>
public class AuthFlowLicenseFileStore
{
    private readonly string _filePath;

    public AuthFlowLicenseFileStore()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var appFolder = Path.Combine(baseDir, "App_Data");

        if (!Directory.Exists(appFolder))
        {
            try
            {
                Directory.CreateDirectory(appFolder);
            }
            catch
            {
                // Fallback to the base directory if App_Data can't be
                // created (e.g. read-only deployment folder).
                appFolder = baseDir;
            }
        }

        _filePath = Path.Combine(appFolder, "authflow-license.lic");
    }

    /// <summary>Full path of the persisted license file (for logging/diagnostics).</summary>
    public string FilePath => _filePath;

    public bool Exists() => File.Exists(_filePath);

    /// <summary>
    /// Reads the persisted license token, or <c>null</c> if none has been
    /// imported yet (or the file is empty/unreadable).
    /// </summary>
    public string? Read()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var content = File.ReadAllText(_filePath).Trim();
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Persists a newly imported license token, overwriting any previous one.
    /// Only call this AFTER a successful activation/check - we never want to
    /// persist a token that failed validation (see
    /// <see cref="AuthFlowLicenseManager.ImportAndActivateAsync"/>).
    /// </summary>
    public void Save(string licenseKeyContent)
    {
        File.WriteAllText(_filePath, licenseKeyContent.Trim());
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
