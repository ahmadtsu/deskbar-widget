// Translated from YASB `com.DesktopInfo` + service snapshot model
// (MIT © 2024 amnweb). One virtual desktop, as of the moment it was read.

namespace DeskbarWidget.Core;

public sealed record DesktopInfo(int Number, Guid Guid, string Name)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Number.ToString() : Name;
}

public sealed class DesktopsUpdatedEventArgs(int index, bool updateButtons) : EventArgs
{
    public int Index { get; } = index;
    public bool UpdateButtons { get; } = updateButtons;
}
