namespace DeskbarWidget.Core;

/// <summary>
/// Policy governing tile display labels for virtual desktops.
/// </summary>
public static class DesktopLabelPolicy
{
    /// <summary>
    /// Determines the display label for a desktop tile according to whether all names
    /// should be shown or only the active (chosen) desktop's name.
    /// If showAllNames is false, only the active desktop displays its name (or number if unnamed);
    /// inactive desktops display their desktop number.
    /// If showAllNames is true, every desktop displays its name (or number if unnamed).
    /// </summary>
    public static string GetLabel(int number, string? name, bool isActive, bool showAllNames)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name) ? number.ToString() : name;
        if (showAllNames || isActive)
        {
            return resolvedName;
        }

        return number.ToString();
    }
}
