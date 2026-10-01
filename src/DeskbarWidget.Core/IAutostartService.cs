// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel) and YASB Reborn (MIT © 2024 amnweb).
// See ATTRIBUTION.md for license details.

namespace DeskbarWidget.Core;

/// <summary>
/// Service managing Windows startup registration via the Run registry key.
/// </summary>
public interface IAutostartService
{
    /// <summary>
    /// Gets or sets whether autostart is enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Checks whether the application is currently registered for autostart.
    /// </summary>
    bool CheckIsEnabled();

    /// <summary>
    /// Enables autostart using the given executable path (or current process path if null).
    /// </summary>
    bool Enable(string? executablePath = null);

    /// <summary>
    /// Disables autostart by removing the registry entry.
    /// </summary>
    bool Disable();

    /// <summary>
    /// Enables or disables autostart according to the specified boolean.
    /// </summary>
    bool SetAutostart(bool enable, string? executablePath = null);
}
