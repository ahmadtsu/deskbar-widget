// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Plain P/Invoke taskbar helpers (user32/shell32 only). All wrappers swallow
/// native failures and return Zero/false/96 instead of throwing: the taskbar
/// may be mid-build (logon, explorer restart) and callers treat that as "not ready".
/// </summary>
internal static class NativeTaskbar
{
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const uint SWP_HIDEWINDOW = 0x0080;

    private const string TaskbarXamlIslandClass = "Windows.UI.Composition.DesktopWindowContentBridge";
    private const int GW_CHILD = 5;
    private const int GW_HWNDNEXT = 2;
    private const int ABM_GETSTATE = 0x00000004;
    private const uint ABS_AUTOHIDE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindow(nint hWnd, int uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindowPrivate(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
    private static extern uint GetWindowThreadProcessIdPrivate(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "ScreenToClient", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClientPrivate(nint hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRectPrivate(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "SetParent", SetLastError = true)]
    private static extern nint SetParentPrivate(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", EntryPoint = "GetParent", SetLastError = true)]
    private static extern nint GetParentPrivate(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPosPrivate(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", EntryPoint = "OpenInputDesktop")]
    private static extern nint OpenInputDesktopPrivate(uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenDesktop")]
    private static extern nint OpenDesktopPrivate(string lpszDesktop, uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", EntryPoint = "SetThreadDesktop")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadDesktopPrivate(nint hDesktop);

    internal static bool EnsureInteractiveDesktop()
    {
        try
        {
            var hDesk = OpenInputDesktopPrivate(0, false, 0x01FF);
            if (hDesk == nint.Zero)
            {
                hDesk = OpenDesktopPrivate("Default", 0, false, 0x01FF);
            }

            if (hDesk != nint.Zero)
            {
                return SetThreadDesktopPrivate(hDesk);
            }
        }
        catch
        {
        }

        return false;
    }

    internal static nint FindTaskbarHandle()
    {
        try
        {
            var handle = FindWindow("Shell_TrayWnd", null);
            if (handle == nint.Zero)
            {
                EnsureInteractiveDesktop();
                handle = FindWindow("Shell_TrayWnd", null);
            }
            return handle;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.FindTaskbarHandle failed: {ex.Message}");
            return nint.Zero;
        }
    }

    internal static string GetWindowClassName(nint hwnd)
    {
        try
        {
            var sb = new StringBuilder(256);
            return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.GetWindowClassName failed: {ex.Message}");
            return string.Empty;
        }
    }

    internal static bool IsSecondaryTaskbar(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return false;
        }

        return string.Equals(GetWindowClassName(hwnd), "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<nint> FindSecondaryTaskbarHandles()
    {
        var list = new List<nint>();
        try
        {
            nint hwnd = nint.Zero;
            while ((hwnd = FindWindowEx(nint.Zero, hwnd, "Shell_SecondaryTrayWnd", null)) != nint.Zero)
            {
                if (IsWindow(hwnd) && IsWindowVisible(hwnd))
                {
                    list.Add(hwnd);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.FindSecondaryTaskbarHandles failed: {ex.Message}");
        }

        return list;
    }

    internal static IReadOnlyList<nint> FindAllTaskbarHandles()
    {
        var list = new List<nint>();
        var primary = FindTaskbarHandle();
        if (primary != nint.Zero && IsWindow(primary))
        {
            list.Add(primary);
        }

        list.AddRange(FindSecondaryTaskbarHandles());
        return list;
    }

    internal static bool IsClassicTaskbar(nint taskbar = 0)
    {
        try
        {
            if (taskbar == nint.Zero)
            {
                taskbar = FindTaskbarHandle();
            }

            if (taskbar == nint.Zero)
            {
                return false;
            }

            return !HasChildOfClass(taskbar, TaskbarXamlIslandClass);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.IsClassicTaskbar failed: {ex.Message}");
            return false;
        }
    }

    internal static bool IsTaskbarAutoHiding()
    {
        try
        {
            var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            uint state = SHAppBarMessage(ABM_GETSTATE, ref data);
            return (state & ABS_AUTOHIDE) == ABS_AUTOHIDE;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.IsTaskbarAutoHiding failed: {ex.Message}");
            return false;
        }
    }

    internal static uint GetDpiForWindow(nint hwnd)
    {
        try
        {
            uint dpi = GetDpiForWindowPrivate(hwnd);
            return dpi == 0 ? 96 : dpi;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.GetDpiForWindow failed: {ex.Message}");
            return 96;
        }
    }

    internal static uint GetWindowThreadProcessId(nint hwnd, out uint processId)
    {
        try
        {
            return GetWindowThreadProcessIdPrivate(hwnd, out processId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.GetWindowThreadProcessId failed: {ex.Message}");
            processId = 0;
            return 0;
        }
    }

    internal static bool ScreenToClient(nint hwnd, ref int x, ref int y)
    {
        try
        {
            var pt = new POINT { X = x, Y = y };
            if (!ScreenToClientPrivate(hwnd, ref pt))
            {
                return false;
            }

            x = pt.X;
            y = pt.Y;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.ScreenToClient failed: {ex.Message}");
            return false;
        }
    }

    internal static bool GetWindowRect(nint hwnd, out RECT rect)
    {
        try
        {
            return GetWindowRectPrivate(hwnd, out rect);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.GetWindowRect failed: {ex.Message}");
            rect = default;
            return false;
        }
    }

    /// <summary>
    /// Checks if the taskbar is visible on its target monitor, accounting for auto-hide.
    /// Adapted from EverythingToolbar TryGetVisibleTaskbarRect (MIT © 2023 Stephan Rumswinkel).
    /// </summary>
    internal static bool TryGetVisibleTaskbarRect(nint taskbarHandle, out System.Windows.Rect taskbarRect)
    {
        taskbarRect = System.Windows.Rect.Empty;
        if (!GetWindowRect(taskbarHandle, out var r))
        {
            return false;
        }

        int taskbarWidth = r.Right - r.Left;
        int taskbarHeight = r.Bottom - r.Top;
        if (taskbarWidth <= 0 || taskbarHeight <= 0)
        {
            return false;
        }

        taskbarRect = new System.Windows.Rect(r.Left, r.Top, taskbarWidth, taskbarHeight);

        if (!IsTaskbarAutoHiding())
        {
            return true;
        }

        var monitor = MonitorFromWindow(taskbarHandle, MONITOR_DEFAULTTONEAREST);
        if (monitor == nint.Zero)
        {
            return true;
        }

        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi))
        {
            return true;
        }

        var mon = mi.rcMonitor;
        int visibleWidth = Math.Max(0, Math.Min(r.Right, mon.Right) - Math.Max(r.Left, mon.Left));
        int visibleHeight = Math.Max(0, Math.Min(r.Bottom, mon.Bottom) - Math.Max(r.Top, mon.Top));

        double dpiScale = GetDpiForWindow(taskbarHandle) / 96.0;
        int tolerance = (int)Math.Ceiling(2 * dpiScale);

        bool isVertical = taskbarHeight > taskbarWidth;
        return isVertical ? visibleWidth >= taskbarWidth - tolerance : visibleHeight >= taskbarHeight - tolerance;
    }

    internal static nint SetParent(nint child, nint newParent)
    {
        try
        {
            return SetParentPrivate(child, newParent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.SetParent failed: {ex.Message}");
            return nint.Zero;
        }
    }

    internal static nint GetParent(nint hwnd)
    {
        try
        {
            return GetParentPrivate(hwnd);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.GetParent failed: {ex.Message}");
            return nint.Zero;
        }
    }

    internal static bool SetWindowPos(nint hwnd, int x, int y, int cx, int cy, uint flags)
    {
        try
        {
            return SetWindowPosPrivate(hwnd, nint.Zero, x, y, cx, cy, flags);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NativeTaskbar.SetWindowPos failed: {ex.Message}");
            return false;
        }
    }

    private static bool HasChildOfClass(nint parentHandle, string className)
    {
        nint child = GetWindow(parentHandle, GW_CHILD);
        var sb = new StringBuilder(256);
        while (child != nint.Zero)
        {
            sb.Clear();
            if (GetClassName(child, sb, sb.Capacity) > 0 && sb.ToString() == className)
            {
                return true;
            }

            child = GetWindow(child, GW_HWNDNEXT);
        }

        return false;
    }
}
