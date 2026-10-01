using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.App;

/// <summary>
/// User preferences persisted in HKCU\Software\DeskbarWidget and Windows startup settings.
/// </summary>
internal static class AppSettings
{
    private const string SettingsKey = @"Software\DeskbarWidget";
    private const string ShowAllDesktopNamesValue = "ShowAllDesktopNames";
    private const string AutostartEnabledValue = "AutostartEnabled";

    private static readonly AutostartService _autostartService = new();

    /// <summary>
    /// Gets or sets whether to display names for every desktop, or only for the active desktop.
    /// </summary>
    public static bool ShowAllDesktopNames
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                return (int?)key?.GetValue(ShowAllDesktopNamesValue, 0) != 0;
            }
            catch
            {
                return false;
            }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key?.SetValue(ShowAllDesktopNamesValue, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                DeskLogger.Info($"Failed to save settings: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the application starts automatically with Windows logon.
    /// Defaults to true on fresh install / first run.
    /// Persisted in HKCU\Software\DeskbarWidget ("AutostartEnabled") and HKCU\...\CurrentVersion\Run.
    /// </summary>
    public static bool AutostartEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                var raw = key?.GetValue(AutostartEnabledValue);
                if (raw is null)
                {
                    // Default behaviour: enabled on startup
                    return true;
                }

                return (int)raw != 0;
            }
            catch
            {
                return true;
            }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key?.SetValue(AutostartEnabledValue, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                DeskLogger.Info($"Failed to save settings: {ex.Message}");
            }

            _autostartService.SetAutostart(value);
        }
    }
}
