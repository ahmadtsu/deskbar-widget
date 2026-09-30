using Microsoft.Win32;

namespace DeskbarWidget.App;

/// <summary>
/// User preferences persisted in HKCU\Software\DeskbarWidget.
/// </summary>
internal static class AppSettings
{
    private const string SettingsKey = @"Software\DeskbarWidget";
    private const string ShowAllDesktopNamesValue = "ShowAllDesktopNames";

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
}
