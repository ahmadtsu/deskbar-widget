// Translated from YASB virtual-desktop error types (MIT © 2024 amnweb).

namespace DeskbarWidget.Interop;

public class VirtualDesktopException : Exception
{
    public VirtualDesktopException(string message)
        : base(message)
    {
    }

    public VirtualDesktopException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

internal sealed class VirtualDesktopUnsupportedException : VirtualDesktopException
{
    public VirtualDesktopUnsupportedException(string message)
        : base(message)
    {
    }
}
