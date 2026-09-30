// Translated from YASB `notification.py` (MIT © 2024 amnweb). See ATTRIBUTION.md.
// One COM sink implementing every notification IID; the shell calls whichever
// vtable matches this build. Parameters are ignored (layouts vary by build);
// consumers re-read state through VirtualDesktopApi. Never throws across COM.

using System.Runtime.InteropServices;
using DeskbarWidget.Interop;

namespace DeskbarWidget.Core;

[ComVisible(true)]
[Guid("7A6B6C5E-4F2A-4E8B-9C1D-3E5A7B9D1F01")]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class DesktopNotificationSink :
    INotification23H2A,
    INotification23H2B,
    INotification21H2,
    INotificationWin10,
    INotification2
{
    private readonly Action<DesktopEvent> _dispatch;

    public DesktopNotificationSink(Action<DesktopEvent> dispatch)
    {
        _dispatch = dispatch;
    }

    private int Emit(DesktopEvent e)
    {
        try
        {
            _dispatch(e);
        }
        catch
        {
            // Swallow: a failed HRESULT can make the shell drop the registration.
        }

        return 0;
    }

    // ---- 23H2 A ----
    int INotification23H2A.VirtualDesktopCreated(IntPtr p0) => Emit(DesktopEvent.Created);
    int INotification23H2A.VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1) => 0;
    int INotification23H2A.VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1) => 0;
    int INotification23H2A.VirtualDesktopDestroyed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Destroyed);
    int INotification23H2A.VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2) => Emit(DesktopEvent.Moved);
    int INotification23H2A.VirtualDesktopRenamed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Renamed);
    int INotification23H2A.ViewVirtualDesktopChanged(IntPtr p0) => 0;
    int INotification23H2A.CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.CurrentChanged);
    int INotification23H2A.VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.WallpaperChanged);
    int INotification23H2A.VirtualDesktopSwitched(IntPtr p0) => Emit(DesktopEvent.Switched);
    int INotification23H2A.RemoteVirtualDesktopConnected(IntPtr p0) => 0;

    // ---- 23H2 B ----
    int INotification23H2B.VirtualDesktopCreated(IntPtr p0) => Emit(DesktopEvent.Created);
    int INotification23H2B.VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1) => 0;
    int INotification23H2B.VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1) => 0;
    int INotification23H2B.VirtualDesktopDestroyed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Destroyed);
    int INotification23H2B.VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2) => Emit(DesktopEvent.Moved);
    int INotification23H2B.VirtualDesktopRenamed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Renamed);
    int INotification23H2B.ViewVirtualDesktopChanged(IntPtr p0) => 0;
    int INotification23H2B.CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.CurrentChanged);
    int INotification23H2B.VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.WallpaperChanged);
    int INotification23H2B.VirtualDesktopSwitched(IntPtr p0) => Emit(DesktopEvent.Switched);
    int INotification23H2B.RemoteVirtualDesktopConnected(IntPtr p0) => 0;

    // ---- 21H2 ----
    int INotification21H2.VirtualDesktopCreated(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Created);
    int INotification21H2.VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1, IntPtr p2) => 0;
    int INotification21H2.VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1, IntPtr p2) => 0;
    int INotification21H2.VirtualDesktopDestroyed(IntPtr p0, IntPtr p1, IntPtr p2) => Emit(DesktopEvent.Destroyed);
    int INotification21H2.Proc7(IntPtr p0) => 0;
    int INotification21H2.VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3) => Emit(DesktopEvent.Moved);
    int INotification21H2.VirtualDesktopRenamed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Renamed);
    int INotification21H2.ViewVirtualDesktopChanged(IntPtr p0) => 0;
    int INotification21H2.CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1, IntPtr p2) => Emit(DesktopEvent.CurrentChanged);
    int INotification21H2.VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.WallpaperChanged);

    // ---- Win10 ----
    int INotificationWin10.VirtualDesktopCreated(IntPtr p0) => Emit(DesktopEvent.Created);
    int INotificationWin10.VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1) => 0;
    int INotificationWin10.VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1) => 0;
    int INotificationWin10.VirtualDesktopDestroyed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Destroyed);
    int INotificationWin10.ViewVirtualDesktopChanged(IntPtr p0) => 0;
    int INotificationWin10.CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.CurrentChanged);

    // ---- Notification2 (19041 rename route) ----
    int INotification2.VirtualDesktopCreated(IntPtr p0) => Emit(DesktopEvent.Created);
    int INotification2.VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1) => 0;
    int INotification2.VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1) => 0;
    int INotification2.VirtualDesktopDestroyed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Destroyed);
    int INotification2.ViewVirtualDesktopChanged(IntPtr p0) => 0;
    int INotification2.CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.CurrentChanged);
    int INotification2.VirtualDesktopRenamed(IntPtr p0, IntPtr p1) => Emit(DesktopEvent.Renamed);
}

internal sealed class DesktopNotificationListener : IDisposable
{
    private readonly Action<DesktopEvent> _onEvent;
    private readonly VirtualDesktopApi _api;
    private IVirtualDesktopNotificationService? _service;
    private DesktopNotificationSink? _sink;
    private uint _cookie;
    private bool _disposed;

    public DesktopNotificationListener(Action<DesktopEvent> onEvent, VirtualDesktopApi? api = null)
    {
        _onEvent = onEvent;
        _api = api ?? new VirtualDesktopApi();
    }

    public bool Active => _cookie != 0;

    public bool Start()
    {
        if (Active)
        {
            return true;
        }

        try
        {
            var tier = new TierResolver().Resolve();
            Type iface = tier switch
            {
                VirtualDesktopTiers.V26100 => typeof(INotification23H2A),
                VirtualDesktopTiers.V22631 => typeof(INotification23H2A),
                VirtualDesktopTiers.V22621 => typeof(INotification23H2B),
                _ when tier >= VirtualDesktopTiers.V20231 => typeof(INotification21H2),
                _ => typeof(INotificationWin10),
            };

            _service = _api.NotificationService();
            _sink = new DesktopNotificationSink(_onEvent);
            var ptr = Marshal.GetComInterfaceForObject(_sink, iface);
            try
            {
                var hr = _service.Register(ptr, out _cookie);
                if (hr != 0 || _cookie == 0)
                {
                    _cookie = 0;
                    _sink = null;
                    _service = null;
                    return false;
                }
            }
            finally
            {
                Marshal.Release(ptr);
            }

            return true;
        }
        catch
        {
            _cookie = 0;
            _sink = null;
            _service = null;
            return false;
        }
    }

    public void Stop()
    {
        if (_cookie != 0 && _service is not null)
        {
            try
            {
                _service.Unregister(_cookie);
            }
            catch
            {
                // Best effort.
            }
        }

        _cookie = 0;
        _service = null;
        _sink = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}
