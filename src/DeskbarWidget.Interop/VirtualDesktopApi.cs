// Translated from YASB `com.py` VirtualDesktopApi (MIT © 2024 amnweb).
// Plain-value wrapper: nothing above this layer holds a COM pointer.
// Desktops are addressed by GUID (single FindDesktop); only List enumerates.
// v1 surface: list / current / count / switch + capability flags. Move/pin/
// wallpaper arrive with later phases.

using System.Runtime.InteropServices;

namespace DeskbarWidget.Interop;

public sealed record DesktopData(int Number, Guid Guid, string Name);

public sealed class VirtualDesktopApi
{
    private const uint ASFW_ANY = 0xFFFFFFFF;

    private readonly ThreadLocal<State?> _state = new(trackAllValues: false);
    private readonly TierResolver _tiers = new();

    public bool SupportsNames => ThreadSupportsNames();
    public bool SupportsWallpaper => TierSupportsWallpaper();

    private int Tier => Ensure().Tier;

    private bool ThreadSupportsNames()
    {
        var s = Ensure();
        return VirtualDesktopTiers.SupportsNames(s.Tier, Environment.OSVersion.Version.Build);
    }

    private bool TierSupportsWallpaper() => VirtualDesktopTiers.SupportsWallpaper(Ensure().Tier);

    public bool Available()
    {
        try
        {
            Ensure();
            return true;
        }
        catch (Exception ex) when (ex is VirtualDesktopException or COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IReadOnlyList<DesktopData> ListDesktops()
    {
        var s = Ensure();
        var items = new List<DesktopData>();
        switch (s.Tier)
        {
            case VirtualDesktopTiers.V26100:
                CollectModern(s.Manager26100!, items, modern: true);
                break;
            case VirtualDesktopTiers.V22631:
                CollectModern(s.Manager22631!, items, modern: true);
                break;
            case VirtualDesktopTiers.V22621:
                CollectModern(s.Manager22621!, items, modern: true);
                break;
            case VirtualDesktopTiers.V22449:
            case VirtualDesktopTiers.V21313:
                Collect21313(s.Manager21313!, items);
                break;
            case VirtualDesktopTiers.V20231:
                Collect20231(s.Manager20231!);
                Collect20231Into(s.Manager20231!, items);
                break;
            default:
                CollectWin10(s.ManagerWin10!, items);
                break;
        }

        return items;
    }

    public Guid CurrentGuid()
    {
        var s = Ensure();
        return s.Tier switch
        {
            VirtualDesktopTiers.V26100 => GetIdModern(GetCurrent26100(s)),
            VirtualDesktopTiers.V22631 => GetIdModern(GetCurrent22631(s)),
            VirtualDesktopTiers.V22621 => GetIdModern(GetCurrent22621(s)),
            VirtualDesktopTiers.V22449 or VirtualDesktopTiers.V21313 => GetId21313(GetCurrent21313(s)),
            VirtualDesktopTiers.V20231 => GetId20231(GetCurrent20231(s)),
            _ => GetIdWin10(GetCurrentWin10(s)),
        };
    }

    public int Count() => ListDesktops().Count;

    public void SwitchTo(Guid guid)
    {
        var s = Ensure();
        try
        {
            NativeHString.AllowSetForegroundWindow(ASFW_ANY);
        }
        catch
        {
            // Best effort; the switch itself matters.
        }

        switch (s.Tier)
        {
            case VirtualDesktopTiers.V26100:
            {
                var m = s.Manager26100!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(d), "SwitchDesktop");
                break;
            }
            case VirtualDesktopTiers.V22631:
            {
                var m = s.Manager22631!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(d), "SwitchDesktop");
                break;
            }
            case VirtualDesktopTiers.V22621:
            {
                var m = s.Manager22621!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(d), "SwitchDesktop");
                break;
            }
            case VirtualDesktopTiers.V22449:
            case VirtualDesktopTiers.V21313:
            {
                var m = s.Manager21313!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(IntPtr.Zero, d), "SwitchDesktop");
                break;
            }
            case VirtualDesktopTiers.V20231:
            {
                var m = s.Manager20231!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(IntPtr.Zero, d), "SwitchDesktop");
                break;
            }
            default:
            {
                var m = s.ManagerWin10!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SwitchDesktop(d), "SwitchDesktop");
                break;
            }
        }
    }

    /// <summary>
    /// Delete a desktop (YASB com.remove). Falls back to the first remaining
    /// desktop when the deleted one is in view; refuses the last desktop.
    /// </summary>
    public void Remove(Guid guid)
    {
        var s = Ensure();
        var fallback = ListDesktops().FirstOrDefault(d => d.Guid != guid);
        if (fallback is null)
        {
            throw new VirtualDesktopException("Cannot remove the only remaining desktop.");
        }

        switch (s.Tier)
        {
            case VirtualDesktopTiers.V26100:
            {
                var m = s.Manager26100!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
            case VirtualDesktopTiers.V22631:
            {
                var m = s.Manager22631!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
            case VirtualDesktopTiers.V22621:
            {
                var m = s.Manager22621!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
            case VirtualDesktopTiers.V22449:
            case VirtualDesktopTiers.V21313:
            {
                var m = s.Manager21313!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
            case VirtualDesktopTiers.V20231:
            {
                var m = s.Manager20231!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
            default:
            {
                var m = s.ManagerWin10!;
                var g = guid;
                var f = fallback.Guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var destroy), "FindDesktop");
                ThrowIfFailed(m.FindDesktop(ref f, out var keep), "FindDesktop");
                ThrowIfFailed(m.RemoveDesktop(destroy, keep), "RemoveDesktop");
                break;
            }
        }
    }

    /// <summary>
    /// Rename a desktop (YASB com.rename).
    /// </summary>
    public void Rename(Guid guid, string name)
    {
        var s = Ensure();
        using var hName = new NativeHString.Owned(name);

        switch (s.Tier)
        {
            case VirtualDesktopTiers.V26100:
            {
                var m = s.Manager26100!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SetName(d, hName.Handle), "SetName");
                break;
            }
            case VirtualDesktopTiers.V22631:
            {
                var m = s.Manager22631!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SetName(d, hName.Handle), "SetName");
                break;
            }
            case VirtualDesktopTiers.V22621:
            {
                var m = s.Manager22621!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SetName(d, hName.Handle), "SetName");
                break;
            }
            case VirtualDesktopTiers.V22449:
            case VirtualDesktopTiers.V21313:
            {
                var m = s.Manager21313!;
                var g = guid;
                ThrowIfFailed(m.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(m.SetName(d, hName.Handle), "SetName");
                break;
            }
            default:
            {
                if (s.ManagerInternal2 is null)
                {
                    throw new VirtualDesktopException("Renaming desktops is not supported on this Windows build.");
                }
                var g = guid;
                ThrowIfFailed(s.ManagerWin10!.FindDesktop(ref g, out var d), "FindDesktop");
                ThrowIfFailed(s.ManagerInternal2.SetName(d, hName.Handle), "SetName");
                break;
            }
        }
    }

    internal IVirtualDesktopNotificationService NotificationService()
    {
        var provider = CreateProvider();
        try
        {
            var clsid = VirtualDesktopGuids.CLSID_VirtualDesktopNotificationService;
            var iid = VirtualDesktopGuids.IID_IVirtualDesktopNotificationService;
            ThrowIfFailed(provider.QueryService(ref clsid, ref iid, out var obj), "NotificationService");
            return (IVirtualDesktopNotificationService)Marshal.GetObjectForIUnknown(obj);
        }
        finally
        {
            Marshal.ReleaseComObject(provider);
        }
    }

    // ---- collectors ----

    private void CollectModern(object manager, List<DesktopData> items, bool modern)
    {
        var array = GetDesktopsModern(manager);
        try
        {
            ThrowIfFailed(array.GetCount(out var count), "GetDesktops.GetCount");
            var iid = VirtualDesktopGuids.IID_IVirtualDesktop_26100;
            for (uint i = 0; i < count; i++)
            {
                ThrowIfFailed(array.GetAt(i, ref iid, out var ptr), "GetAt");
                var d = (IVirtualDesktopModern)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVirtualDesktopModern));
                try
                {
                    ThrowIfFailed(d.GetID(out var guid), "GetID");
                    var name = string.Empty;
                    if (modern && VirtualDesktopTiers.SupportsNames(Tier, Environment.OSVersion.Version.Build))
                    {
                        if (d.GetName(out var h) == 0)
                        {
                            name = NativeHString.Read(h).Trim();
                        }
                    }

                    items.Add(new DesktopData((int)i + 1, guid, name));
                }
                finally
                {
                    Marshal.ReleaseComObject(d);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(array);
        }
    }

    private IObjectArray GetDesktopsModern(object manager)
    {
        var s = Ensure();
        IObjectArray array;
        if (s.Tier == VirtualDesktopTiers.V26100)
        {
            ThrowIfFailed(s.Manager26100!.GetDesktops(out array), "GetDesktops");
        }
        else if (s.Tier == VirtualDesktopTiers.V22631)
        {
            ThrowIfFailed(s.Manager22631!.GetDesktops(out array), "GetDesktops");
        }
        else
        {
            ThrowIfFailed(s.Manager22621!.GetDesktops(out array), "GetDesktops");
        }

        return array;
    }

    private void Collect21313(IManager21313Family m, List<DesktopData> items)
    {
        ThrowIfFailed(m.GetDesktops(IntPtr.Zero, out var array), "GetDesktops");
        try
        {
            ThrowIfFailed(array.GetCount(out var count), "GetCount");
            var iid = VirtualDesktopGuids.IID_IVirtualDesktop_21313;
            var named = VirtualDesktopTiers.SupportsNames(Tier, Environment.OSVersion.Version.Build);
            for (uint i = 0; i < count; i++)
            {
                ThrowIfFailed(array.GetAt(i, ref iid, out var ptr), "GetAt");
                var d = (IVirtualDesktop21313)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVirtualDesktop21313));
                try
                {
                    ThrowIfFailed(d.GetID(out var guid), "GetID");
                    var name = string.Empty;
                    if (named && d.GetName(out var h) == 0)
                    {
                        name = NativeHString.Read(h).Trim();
                    }

                    items.Add(new DesktopData((int)i + 1, guid, name));
                }
                finally
                {
                    Marshal.ReleaseComObject(d);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(array);
        }
    }

    private void Collect20231(IManager20231 m)
    {
        // Existence check kept separate so the visible method stays small;
        // real collection happens in Collect20231Into.
        ThrowIfFailed(m.GetCount(IntPtr.Zero, out _), "GetCount");
    }

    private void Collect20231Into(IManager20231 m, List<DesktopData> items)
    {
        ThrowIfFailed(m.GetDesktops(IntPtr.Zero, out var array), "GetDesktops");
        try
        {
            ThrowIfFailed(array.GetCount(out var count), "GetCount");
            var iid = VirtualDesktopGuids.IID_IVirtualDesktop_20231;
            var named = VirtualDesktopTiers.SupportsNames(Tier, Environment.OSVersion.Version.Build);
            for (uint i = 0; i < count; i++)
            {
                ThrowIfFailed(array.GetAt(i, ref iid, out var ptr), "GetAt");
                var d = (IVirtualDesktop20231)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVirtualDesktop20231));
                try
                {
                    ThrowIfFailed(d.GetID(out var guid), "GetID");
                    items.Add(new DesktopData((int)i + 1, guid, string.Empty));
                    _ = named;
                }
                finally
                {
                    Marshal.ReleaseComObject(d);
                }
            }

            // Pre-21313 names arrive via IVirtualDesktop2; v1 shows numbers there.
        }
        finally
        {
            Marshal.ReleaseComObject(array);
        }
    }

    private void CollectWin10(IManagerWin10 m, List<DesktopData> items)
    {
        ThrowIfFailed(m.GetDesktops(out var array), "GetDesktops");
        try
        {
            ThrowIfFailed(array.GetCount(out var count), "GetCount");
            var iid = VirtualDesktopGuids.IID_IVirtualDesktop_9000;
            for (uint i = 0; i < count; i++)
            {
                ThrowIfFailed(array.GetAt(i, ref iid, out var ptr), "GetAt");
                var d = (IVirtualDesktopWin10)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVirtualDesktopWin10));
                try
                {
                    ThrowIfFailed(d.GetID(out var guid), "GetID");
                    items.Add(new DesktopData((int)i + 1, guid, string.Empty));
                }
                finally
                {
                    Marshal.ReleaseComObject(d);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(array);
        }
    }

    // ---- current helpers ----

    private static IVirtualDesktopModern GetCurrent26100(State s)
    {
        ThrowIfFailed(s.Manager26100!.GetCurrentDesktop(out var d), "GetCurrentDesktop");
        return d;
    }

    private static IVirtualDesktopModern GetCurrent22631(State s)
    {
        ThrowIfFailed(s.Manager22631!.GetCurrentDesktop(out var d), "GetCurrentDesktop");
        return d;
    }

    private static IVirtualDesktopModern GetCurrent22621(State s)
    {
        ThrowIfFailed(s.Manager22621!.GetCurrentDesktop(out var d), "GetCurrentDesktop");
        return d;
    }

    private static IVirtualDesktop21313 GetCurrent21313(State s)
    {
        ThrowIfFailed(s.Manager21313!.GetCurrentDesktop(IntPtr.Zero, out var d), "GetCurrentDesktop");
        return d;
    }

    private static IVirtualDesktop20231 GetCurrent20231(State s)
    {
        ThrowIfFailed(s.Manager20231!.GetCurrentDesktop(IntPtr.Zero, out var d), "GetCurrentDesktop");
        return d;
    }

    private static IVirtualDesktopWin10 GetCurrentWin10(State s)
    {
        ThrowIfFailed(s.ManagerWin10!.GetCurrentDesktop(out var d), "GetCurrentDesktop");
        return d;
    }

    private static Guid GetIdModern(IVirtualDesktopModern d)
    {
        try
        {
            ThrowIfFailed(d.GetID(out var guid), "GetID");
            return guid;
        }
        finally
        {
            Marshal.ReleaseComObject(d);
        }
    }

    private static Guid GetId21313(IVirtualDesktop21313 d)
    {
        try
        {
            ThrowIfFailed(d.GetID(out var guid), "GetID");
            return guid;
        }
        finally
        {
            Marshal.ReleaseComObject(d);
        }
    }

    private static Guid GetId20231(IVirtualDesktop20231 d)
    {
        try
        {
            ThrowIfFailed(d.GetID(out var guid), "GetID");
            return guid;
        }
        finally
        {
            Marshal.ReleaseComObject(d);
        }
    }

    private static Guid GetIdWin10(IVirtualDesktopWin10 d)
    {
        try
        {
            ThrowIfFailed(d.GetID(out var guid), "GetID");
            return guid;
        }
        finally
        {
            Marshal.ReleaseComObject(d);
        }
    }

    // ---- apartment state ----

    private State Ensure()
    {
        var s = _state.Value;
        if (s is not null)
        {
            return s;
        }

        try
        {
            NativeCom.CoInitializeEx(IntPtr.Zero, NativeCom.COINIT_APARTMENTTHREADED);
        }
        catch
        {
            // Already in an MTA; fine for our purposes (mirrors YASB).
        }

        var tier = _tiers.Resolve();
        var provider = CreateProvider();
        try
        {
            s = new State { Tier = tier };
            var svc = VirtualDesktopGuids.CLSID_VirtualDesktopManagerInternalService;
            switch (tier)
            {
                case VirtualDesktopTiers.V26100:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_26100;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "Manager26100");
                    s.Manager26100 = (IManager26100)Marshal.GetObjectForIUnknown(p);
                    break;
                }
                case VirtualDesktopTiers.V22631:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_22631;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "Manager22631");
                    s.Manager22631 = (IManager22631)Marshal.GetObjectForIUnknown(p);
                    break;
                }
                case VirtualDesktopTiers.V22621:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_22621;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "Manager22621");
                    s.Manager22621 = (IManager22621)Marshal.GetObjectForIUnknown(p);
                    break;
                }
                case VirtualDesktopTiers.V22449:
                case VirtualDesktopTiers.V21313:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_21313;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "Manager21313");
                    s.Manager21313 = (IManager21313Family)Marshal.GetObjectForIUnknown(p);
                    break;
                }
                case VirtualDesktopTiers.V20231:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_20231;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "Manager20231");
                    s.Manager20231 = (IManager20231)Marshal.GetObjectForIUnknown(p);
                    break;
                }
                default:
                {
                    var iid = VirtualDesktopGuids.IID_Manager_9000;
                    ThrowIfFailed(provider.QueryService(ref svc, ref iid, out var p), "ManagerWin10");
                    s.ManagerWin10 = (IManagerWin10)Marshal.GetObjectForIUnknown(p);
                    break;
                }
            }

            if (tier < VirtualDesktopTiers.V21313)
            {
                var iid2 = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal2;
                if (provider.QueryService(ref svc, ref iid2, out var p2) == 0 && p2 != IntPtr.Zero)
                {
                    s.ManagerInternal2 = (IManagerInternal2)Marshal.GetObjectForIUnknown(p2);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(provider);
        }

        _state.Value = s;
        return s;
    }

    private static IServiceProvider CreateProvider()
    {
        var clsid = VirtualDesktopGuids.CLSID_ImmersiveShell;
        var iid = VirtualDesktopGuids.IID_IServiceProvider;
        var hr = NativeCom.CoCreateInstance(
            ref clsid, IntPtr.Zero, NativeCom.CLSCTX_LOCAL_SERVER, ref iid, out var obj);
        if (hr != 0 || obj == IntPtr.Zero)
        {
            throw new VirtualDesktopException($"ImmersiveShell unavailable (HRESULT 0x{hr:X8}).");
        }

        return (IServiceProvider)Marshal.GetObjectForIUnknown(obj);
    }

    internal static void ThrowIfFailed(int hr, string op)
    {
        if (hr != 0)
        {
            throw new VirtualDesktopException($"{op} failed (HRESULT 0x{(uint)hr:X8}).");
        }
    }

    private sealed class State
    {
        public int Tier;
        public IManager26100? Manager26100;
        public IManager22631? Manager22631;
        public IManager22621? Manager22621;
        public IManager21313Family? Manager21313;
        public IManager20231? Manager20231;
        public IManagerWin10? ManagerWin10;
        public IManagerInternal2? ManagerInternal2;
    }
}
