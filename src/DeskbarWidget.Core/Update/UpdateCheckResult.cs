namespace DeskbarWidget.Core;

/// <summary>
/// Result of querying GitHub releases for application updates.
/// </summary>
public sealed record UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }
    public Version CurrentVersion { get; init; } = new(1, 0, 0);
    public Version? LatestVersion { get; init; }
    public string? LatestTag { get; init; }
    public string ReleaseUrl { get; init; } = "https://github.com/ahmadtsu/deskbar-widget/releases/latest";
    public string? ReleaseTitle { get; init; }
    public string? ReleaseNotes { get; init; }
    public bool IsSkipped { get; init; }
    public string? ErrorMessage { get; init; }

    public static UpdateCheckResult UpToDate(Version currentVersion, Version latestVersion, string tag, string releaseUrl) =>
        new()
        {
            IsUpdateAvailable = false,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            LatestTag = tag,
            ReleaseUrl = releaseUrl
        };

    public static UpdateCheckResult UpdateAvailable(
        Version currentVersion,
        Version latestVersion,
        string tag,
        string releaseUrl,
        string? title = null,
        string? notes = null) =>
        new()
        {
            IsUpdateAvailable = true,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            LatestTag = tag,
            ReleaseUrl = releaseUrl,
            ReleaseTitle = title,
            ReleaseNotes = notes
        };

    public static UpdateCheckResult Skipped(Version currentVersion, Version latestVersion, string tag, string releaseUrl) =>
        new()
        {
            IsUpdateAvailable = false,
            IsSkipped = true,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            LatestTag = tag,
            ReleaseUrl = releaseUrl
        };

    public static UpdateCheckResult Failed(Version currentVersion, string errorMessage, string releaseUrl = "https://github.com/ahmadtsu/deskbar-widget/releases/latest") =>
        new()
        {
            IsUpdateAvailable = false,
            CurrentVersion = currentVersion,
            ErrorMessage = errorMessage,
            ReleaseUrl = releaseUrl
        };
}
