namespace DeskbarWidget.Core;

/// <summary>
/// Configuration model for DeskbarWidget preferences and customization.
/// Persisted in JSON format (e.g. %USERPROFILE%\.config\DeskbarWidget\config.json).
/// </summary>
public sealed class DeskbarConfig
{
    /// <summary>
    /// Gets or sets whether to display names for every desktop, or only for the active desktop.
    /// </summary>
    public bool ShowAllDesktopNames { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the application starts automatically with Windows logon.
    /// </summary>
    public bool AutostartEnabled { get; set; } = true;

    /// <summary>
    /// Preferred font family for desktop labels and widget text.
    /// </summary>
    public string Font { get; set; } = "Segoe UI Variable Display";

    /// <summary>
    /// Optional custom glyphs or Nerd Font icon mappings per desktop name or number (e.g. {"1": "󰈹", "Code": "󰨞"}).
    /// </summary>
    public Dictionary<string, string>? DesktopIcons { get; set; }

    /// <summary>
    /// Optional theme override name or path to custom styling file.
    /// </summary>
    public string? CustomTheme { get; set; }

    /// <summary>
    /// Creates a deep copy of the configuration object.
    /// </summary>
    public DeskbarConfig Clone()
    {
        return new DeskbarConfig
        {
            ShowAllDesktopNames = ShowAllDesktopNames,
            AutostartEnabled = AutostartEnabled,
            Font = Font,
            DesktopIcons = DesktopIcons is not null ? new Dictionary<string, string>(DesktopIcons) : null,
            CustomTheme = CustomTheme
        };
    }
}
