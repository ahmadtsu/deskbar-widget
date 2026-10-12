using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DeskbarWidget.Core;

/// <summary>
/// Service checking GitHub releases for updates and opening release pages,
/// modeled after EverythingToolbar's update workflow.
/// </summary>
public sealed class UpdateService : IDisposable
{
    public const string DefaultApiUrl = "https://api.github.com/repos/ahmadtsu/deskbar-widget/releases/latest";
    public const string DefaultReleaseUrl = "https://github.com/ahmadtsu/deskbar-widget/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly string _apiUrl;
    private readonly string _fallbackReleaseUrl;

    public Action<string>? Logger { get; set; }

    public UpdateService(
        HttpClient? httpClient = null,
        string apiUrl = DefaultApiUrl,
        string fallbackReleaseUrl = DefaultReleaseUrl,
        Action<string>? logger = null)
    {
        _apiUrl = apiUrl;
        _fallbackReleaseUrl = fallbackReleaseUrl;
        Logger = logger;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            var version = GetCurrentAssemblyVersion().ToString(3);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"DeskbarWidget/{version} (Windows; +https://github.com/ahmadtsu/deskbar-widget)");
            _disposeClient = true;
        }
    }

    /// <summary>
    /// Gets the running application assembly version.
    /// </summary>
    public static Version GetCurrentAssemblyVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetName().Version ?? new Version(1, 0, 0);
    }

    /// <summary>
    /// Checks GitHub for the latest release and determines if an update is available.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        Version? currentVersion = null,
        string? skippedVersion = null,
        bool ignoreSkipped = false,
        CancellationToken cancellationToken = default)
    {
        var current = currentVersion ?? GetCurrentAssemblyVersion();

        try
        {
            using var response = await _httpClient.GetAsync(_apiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                var reason = response.ReasonPhrase ?? "Unknown error";
                Log($"Update check failed with HTTP {code} ({reason}).");
                return UpdateCheckResult.Failed(current, $"HTTP {code}: {reason}", _fallbackReleaseUrl);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                Log("Update check response did not contain a valid release tag.");
                return UpdateCheckResult.Failed(current, "Invalid release response from GitHub.", _fallbackReleaseUrl);
            }

            var latestVersion = ParseVersionFromTag(release.TagName);
            if (latestVersion is null)
            {
                Log($"Could not parse semantic version from release tag '{release.TagName}'.");
                return UpdateCheckResult.Failed(current, $"Unparseable release tag '{release.TagName}'.", _fallbackReleaseUrl);
            }

            var releaseUrl = !string.IsNullOrWhiteSpace(release.HtmlUrl) ? release.HtmlUrl : _fallbackReleaseUrl;

            // Check if newer than current
            if (latestVersion <= current)
            {
                Log($"Application is up to date (current: {current}, latest: {latestVersion}).");
                return UpdateCheckResult.UpToDate(current, latestVersion, release.TagName, releaseUrl);
            }

            // Check if skipped
            if (!ignoreSkipped && !string.IsNullOrWhiteSpace(skippedVersion))
            {
                var parsedSkipped = ParseVersionFromTag(skippedVersion);
                if (parsedSkipped is not null && parsedSkipped == latestVersion)
                {
                    Log($"Update to {latestVersion} is available but marked as skipped by user.");
                    return UpdateCheckResult.Skipped(current, latestVersion, release.TagName, releaseUrl);
                }
            }

            Log($"New update found: {latestVersion} (tag: {release.TagName}).");
            return UpdateCheckResult.UpdateAvailable(
                current,
                latestVersion,
                release.TagName,
                releaseUrl,
                release.Name,
                release.Body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Exception querying GitHub release API: {ex.Message}");
            return UpdateCheckResult.Failed(current, ex.Message, _fallbackReleaseUrl);
        }
    }

    /// <summary>
    /// Parses a semantic version from a GitHub tag string (e.g. "v1.0.2", "1.0.2", "v1.0.2.1").
    /// </summary>
    public static Version? ParseVersionFromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var cleaned = tag.Trim().TrimStart('v', 'V');
        var match = Regex.Match(cleaned, @"^(\d+)(\.(\d+))?(\.(\d+))?(\.(\d+))?");
        if (!match.Success)
        {
            return null;
        }

        var versionStr = match.Value;
        // System.Version requires at least 2 components (major.minor)
        if (!versionStr.Contains('.'))
        {
            versionStr += ".0";
        }

        return Version.TryParse(versionStr, out var v) ? v : null;
    }

    /// <summary>
    /// Opens the specified release page (or default repository releases page) in the user's default browser.
    /// EverythingToolbar pattern: Process.Start with UseShellExecute = true.
    /// </summary>
    public static bool OpenReleaseUrl(string? url = null)
    {
        try
        {
            var targetUrl = !string.IsNullOrWhiteSpace(url) ? url : DefaultReleaseUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Log(string message) => Logger?.Invoke(message);

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
