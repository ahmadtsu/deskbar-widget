using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// User preferences and configuration management.
/// Backed by ConfigService persisting to %USERPROFILE%\.config\DeskbarWidget\config.json (or %APPDATA%).
/// </summary>
internal static class AppSettings
{
    private static readonly AutostartService _autostartService = new();

    public static ConfigService Service => ConfigService.Instance;

    public static string ConfigFilePath => Service.ConfigFilePath;

    public static string ConfigDirectory => Service.ConfigDirectory;

    /// <summary>
    /// Gets or sets whether to display names for every desktop, or only for the active desktop.
    /// </summary>
    public static bool ShowAllDesktopNames
    {
        get => Service.CurrentConfig.ShowAllDesktopNames;
        set => Service.Update(cfg => cfg.ShowAllDesktopNames = value);
    }

    /// <summary>
    /// Gets or sets whether the application starts automatically with Windows logon.
    /// Defaults to true on fresh install / first run.
    /// </summary>
    public static bool AutostartEnabled
    {
        get => Service.CurrentConfig.AutostartEnabled;
        set
        {
            Service.Update(cfg => cfg.AutostartEnabled = value);
            _autostartService.SetAutostart(value);
        }
    }
}
