using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuthFlow.LicenseClient.Models;
using AuthFlow.LicenseClient.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthFlow.AppKit.Services;

/// <summary>
/// Wraps the AuthFlow SDK's <see cref="IUpdateChecker"/> (already registered
/// by <c>AddAuthFlowLicense</c>) with the pieces needed for a self-updating
/// installer flow: knowing "our own" current version, downloading the
/// installer asset the AuthFlow server points at, optionally verifying its
/// SHA-256 checksum, and launching it so the *installer* (not the running
/// app) handles stopping/replacing the Windows Service - see README.md
/// "Mise à jour depuis l'application".
/// </summary>
public sealed class AppUpdateService
{
    // Matches the public "releases/download" URL shape returned by
    // AuthFlow's /api/updates/check, e.g.
    // https://github.com/{owner}/{repo}/releases/download/{tag}/{fileName}
    private static readonly Regex GitHubReleaseAssetUrlPattern = new(
        @"^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/releases/download/(?<tag>[^/]+)/(?<fileName>[^/]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IUpdateChecker _updateChecker;
    private readonly HttpClient _downloadClient;
    private readonly ILogger<AppUpdateService> _logger;
    private readonly string? _gitHubToken;
    private readonly string _serviceBaseName;

    public AppUpdateService(
        IUpdateChecker updateChecker,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AuthFlowAppKitOptions> options,
        ILogger<AppUpdateService> logger)
    {
        _updateChecker = updateChecker;
        // A plain, un-configured HttpClient (no AuthFlow BaseAddress) since
        // the installer asset is typically hosted on GitHub Releases, not
        // the AuthFlow server itself.
        _downloadClient = httpClientFactory.CreateClient(nameof(AppUpdateService));
        _logger = logger;
        _serviceBaseName = string.IsNullOrWhiteSpace(options.Value.ServiceBaseName)
            ? "App"
            : options.Value.ServiceBaseName;
        // Optional: only needed if the consuming app's GitHub repo hosting
        // installer Releases is PRIVATE. Anonymous requests to a private
        // repo's release asset URL always come back 404 (GitHub hides the
        // resource rather than returning 401/403), so without a token the
        // download step fails even though updateAvailable/downloadUrl
        // detection works fine (the AuthFlow API itself doesn't need to
        // touch GitHub). Configure via `GitHub:Token` in user-secrets/env
        // var - see README.md. RECOMMENDED (and the default for the
        // dotnet-new template): publish installers to a SEPARATE, PUBLIC,
        // code-free "releases" repo instead, so this token is never needed
        // and never has to be distributed to client machines - see
        // README.md "Distribution des installeurs".
        _gitHubToken = configuration["GitHub:Token"];
    }

    /// <summary>
    /// The version of the currently running build, as reported to AuthFlow's
    /// heartbeat too - keeping both in sync avoids "the update banner and
    /// the fleet dashboard disagree" confusion.
    /// </summary>
    /// <remarks>
    /// BUG FIX (found while diagnosing "update button never detects a new
    /// version" against the real AuthFlow server): .NET always normalizes an
    /// assembly's <see cref="Version"/> to 4 components (Major.Minor.Build.Revision),
    /// even when the csproj/CI only set 3 (e.g. -p:AssemblyVersion=1.0.0 becomes
    /// System.Version(1,0,0,0)). <see cref="Version.ToString()"/> then prints
    /// all 4 parts, e.g. "1.0.0.0". The AuthFlow server validates
    /// <c>currentVersion</c> as strict 3-component semver and returns HTTP 400
    /// ("Invalid currentVersion, must be valid semver") for "1.0.0.0" - and the
    /// SDK's <see cref="IUpdateChecker"/> silently converts that error into
    /// "no update available", so the UI would just show "up to date" instead
    /// of any error. Trimming to Major.Minor.Build here fixes it without
    /// touching the shared SDK.
    /// </remarks>
    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null) return "unknown";
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }
    }

    public Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default) =>
        _updateChecker.CheckForUpdateAsync(CurrentVersion, ct);

    /// <summary>
    /// Downloads the installer asset described by <paramref name="result"/>
    /// to a temp file, verifies its SHA-256 checksum when the server
    /// provided one, and launches it via the shell (so Windows prompts for
    /// the UAC elevation the installer needs to register/replace the
    /// Windows Service). Returns once the installer process has been
    /// *started* - it does not wait for the install to finish, since the
    /// installer will stop this app's own service as part of upgrading it.
    /// </summary>
    public async Task DownloadAndLaunchInstallerAsync(UpdateCheckResult result, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            throw new InvalidOperationException("No downloadUrl provided by the AuthFlow update-check response.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"{_serviceBaseName}-Update");
        Directory.CreateDirectory(tempDir);
        var fileName = Path.GetFileName(new Uri(result.DownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"{_serviceBaseName}-Setup-{result.LatestVersion}.exe";
        var destPath = Path.Combine(tempDir, fileName);

        _logger.LogInformation("Downloading update installer {Url} -> {Path}", result.DownloadUrl, destPath);

        await DownloadInstallerFileAsync(result.DownloadUrl, destPath, ct);

        if (!string.IsNullOrWhiteSpace(result.Checksum))
        {
            var actual = await ComputeSha256Async(destPath, ct);
            // The AuthFlow server prefixes the checksum with the algorithm
            // name (e.g. "sha256:4ead6941...") while ComputeSha256Async
            // returns just the bare hex digest - strip any "algo:" prefix
            // before comparing, otherwise this always mismatches even for a
            // perfectly valid download.
            var expected = result.Checksum;
            var colonIndex = expected.IndexOf(':');
            if (colonIndex >= 0)
                expected = expected[(colonIndex + 1)..];

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destPath);
                throw new InvalidOperationException(
                    $"Downloaded installer checksum mismatch (expected {expected}, got {actual}) - refusing to run it.");
            }
            _logger.LogInformation("Installer checksum verified (SHA-256 {Checksum})", actual);
        }
        else
        {
            _logger.LogWarning("AuthFlow update-check response had no checksum - skipping integrity verification.");
        }

        _logger.LogInformation("Launching installer {Path}", destPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = destPath,
            UseShellExecute = true, // lets Windows show the UAC prompt the installer needs
        });
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Downloads the installer to <paramref name="destPath"/>. If the URL
    /// looks like a GitHub "releases/download" asset URL AND a
    /// <c>GitHub:Token</c> is configured, goes through the authenticated
    /// GitHub REST API (needed only if that repo is private - a plain
    /// anonymous GET to that URL 404s in that case). Otherwise falls back to
    /// a direct, anonymous download (the default/recommended path: works for
    /// public repos, e.g. the dedicated "releases" repo created by the
    /// dotnet-new template, or any other host).
    /// </summary>
    private async Task DownloadInstallerFileAsync(string downloadUrl, string destPath, CancellationToken ct)
    {
        var match = GitHubReleaseAssetUrlPattern.Match(downloadUrl);
        if (!match.Success || string.IsNullOrWhiteSpace(_gitHubToken))
        {
            await using var httpStream = await _downloadClient.GetStreamAsync(downloadUrl, ct);
            await using var fileStream = File.Create(destPath);
            await httpStream.CopyToAsync(fileStream, ct);
            return;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        var tag = match.Groups["tag"].Value;
        var fileName = match.Groups["fileName"].Value;

        _logger.LogInformation(
            "Private repo detected - resolving asset {FileName} for {Owner}/{Repo}@{Tag} via the GitHub API.",
            fileName, owner, repo, tag);

        using var releaseRequest = CreateGitHubApiRequest(
            $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{Uri.EscapeDataString(tag)}",
            "application/vnd.github+json");
        using var releaseResponse = await _downloadClient.SendAsync(releaseRequest, ct);
        if (!releaseResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Impossible de lire la release GitHub {tag} ({owner}/{repo}) : HTTP {(int)releaseResponse.StatusCode}. " +
                "Vérifiez que le jeton GitHub:Token est valide et a la permission 'Contents: Read' sur ce dépôt.");
        }

        using var releaseDoc = JsonDocument.Parse(await releaseResponse.Content.ReadAsStreamAsync(ct));
        string? assetApiUrl = null;
        if (releaseDoc.RootElement.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var nameProp) &&
                    string.Equals(nameProp.GetString(), fileName, StringComparison.OrdinalIgnoreCase) &&
                    asset.TryGetProperty("url", out var urlProp))
                {
                    assetApiUrl = urlProp.GetString();
                    break;
                }
            }
        }

        if (assetApiUrl is null)
        {
            throw new InvalidOperationException(
                $"Asset '{fileName}' introuvable dans la release GitHub {tag} de {owner}/{repo}.");
        }

        // Requesting the asset API URL with Accept: application/octet-stream
        // makes GitHub respond with a 302 redirect to a short-lived, public,
        // pre-signed blob-storage URL - HttpClient follows it automatically
        // and (correctly) does NOT forward the Authorization header to that
        // different host, so no long-lived token leaks out with the file.
        using var assetRequest = CreateGitHubApiRequest(assetApiUrl, "application/octet-stream");
        using var assetResponse = await _downloadClient.SendAsync(assetRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!assetResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Échec du téléchargement de l'asset '{fileName}' : HTTP {(int)assetResponse.StatusCode}.");
        }

        await using var downloadStream = await assetResponse.Content.ReadAsStreamAsync(ct);
        await using var outputFileStream = File.Create(destPath);
        await downloadStream.CopyToAsync(outputFileStream, ct);
    }

    private HttpRequestMessage CreateGitHubApiRequest(string url, string acceptHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue($"{_serviceBaseName}-UpdateChecker", "1.0"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _gitHubToken);
        return request;
    }
}
