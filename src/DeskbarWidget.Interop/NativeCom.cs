// Translated from YASB `com_base.py` HSTRING helpers (MIT © 2024 amnweb).
// WinRT strings for desktop names / wallpaper paths. Received HSTRINGs are not
// owned (read-only); created ones are deleted after the call.

using System.Runtime.InteropServices;

namespace DeskbarWidget.Interop;

internal static partial class NativeCom
{
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(IntPtr reserved, uint coInit);

    internal const uint CLSCTX_LOCAL_SERVER = 4;
    internal const uint COINIT_APARTMENTTHREADED = 0x2;
}

internal static partial class NativeHString
{
    [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int WindowsCreateString(
        string source, uint length, out IntPtr hstring);

    [LibraryImport("combase.dll")]
    private static partial int WindowsDeleteString(IntPtr hstring);

    [LibraryImport("combase.dll")]
    private static partial IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    [LibraryImport("user32.dll")]
    internal static partial int AllowSetForegroundWindow(uint dwProcessId);

    internal static string Read(IntPtr hstring)
    {
        if (hstring == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var buffer = WindowsGetStringRawBuffer(hstring, out var length);
            if (buffer == IntPtr.Zero || length == 0)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(buffer, (int)length) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal sealed class Owned : IDisposable
    {
        public IntPtr Handle { get; private set; }

        public Owned(string value)
        {
            Handle = IntPtr.Zero;
            if (!string.IsNullOrEmpty(value))
            {
                WindowsCreateString(value, (uint)value.Length, out var h);
                Handle = h;
            }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    WindowsDeleteString(Handle);
                }
                catch
                {
                    // Best effort; process exit frees it anyway.
                }

                Handle = IntPtr.Zero;
            }
        }
    }
}
