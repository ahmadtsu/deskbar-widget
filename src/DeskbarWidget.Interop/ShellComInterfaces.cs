// Translated from YASB Reborn `interfaces.py` + `com_base.py`
// (MIT © 2024 amnweb; original Yasb MIT © 2021 denBot). See ATTRIBUTION.md.
// Vtable orders mirror YASB's per-tier specs exactly; COM is positional, so do
// not reorder. View pointers are IntPtr (opaque, unused in v1) so we don't need
// the 60-method IApplicationView table yet. HSTRINGs are IntPtr + HStringHelper.

using System.Runtime.InteropServices;

namespace DeskbarWidget.Interop;

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IServiceProvider
{
    [PreserveSig]
    int QueryService(ref Guid service, ref Guid iid, out IntPtr ppv);
}

[ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectArray
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, ref Guid iid, out IntPtr obj);
}

// ---- IVirtualDesktop variants ----

[ComImport, Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopModern
{
    [PreserveSig] int IsViewVisible(IntPtr view, out uint visible);
    [PreserveSig] int GetID(out Guid guid);
    [PreserveSig] int GetName(out IntPtr hName);
    [PreserveSig] int GetWallpaperPath(out IntPtr hPath);
    [PreserveSig] int IsRemote(out IntPtr remote);
}

[ComImport, Guid("536D3495-B208-4CC9-AE26-DE8111275BF8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktop21313
{
    [PreserveSig] int IsViewVisible(IntPtr view, out uint visible);
    [PreserveSig] int GetID(out Guid guid);
    [PreserveSig] int IsRemote(out IntPtr remote);
    [PreserveSig] int GetName(out IntPtr hName);
    [PreserveSig] int GetWallpaperPath(out IntPtr hPath);
}

[ComImport, Guid("62FDF88B-11CA-4AFB-8BD8-2296DFAE49E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktop20231
{
    [PreserveSig] int IsViewVisible(IntPtr view, out uint visible);
    [PreserveSig] int GetID(out Guid guid);
}

[ComImport, Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopWin10
{
    [PreserveSig] int IsViewVisible(IntPtr view, out uint visible);
    [PreserveSig] int GetID(out Guid guid);
}

[ComImport, Guid("31EBDE3F-6EC3-4CBD-B9FB-0EF6D09B41F4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktop2
{
    [PreserveSig] int IsViewVisible(IntPtr view, out uint visible);
    [PreserveSig] int GetID(out Guid guid);
    [PreserveSig] int GetName(out IntPtr hName);
}

// ---- IVirtualDesktopManagerInternal variants (YASB _manager_methods order) ----

[ComImport, Guid("53F5CA0B-158F-4124-900C-057158060B27"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManager26100
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktopModern desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(out IVirtualDesktopModern desktop);
    [PreserveSig] int GetDesktops(out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktopModern from, uint direction, out IVirtualDesktopModern adjacent);
    [PreserveSig] int SwitchDesktop(IVirtualDesktopModern desktop);
    [PreserveSig] int SwitchDesktopAndMoveForegroundView(IVirtualDesktopModern desktop);
    [PreserveSig] int CreateDesktopW(out IVirtualDesktopModern desktop);
    [PreserveSig] int MoveDesktop(IVirtualDesktopModern desktop, uint index);
    [PreserveSig] int RemoveDesktop(IVirtualDesktopModern destroy, IVirtualDesktopModern fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktopModern desktop);
    [PreserveSig] int GetDesktopSwitchIncludeExcludeViews(IVirtualDesktopModern desktop, out IObjectArray a, out IObjectArray b);
    [PreserveSig] int SetName(IVirtualDesktopModern desktop, IntPtr hName);
    [PreserveSig] int SetWallpaper(IVirtualDesktopModern desktop, IntPtr hPath);
    [PreserveSig] int SetWallpaperForAllDesktops(IntPtr hPath);
    [PreserveSig] int CopyDesktopState(IntPtr view0, IntPtr view1);
    [PreserveSig] int CreateRemoteDesktop(IntPtr hStr, out IVirtualDesktopModern desktop);
    [PreserveSig] int pDesktop(IVirtualDesktopModern desktop);
    [PreserveSig] int SwitchRemoteDesktop(IVirtualDesktopModern desktop, uint unknown);
    [PreserveSig] int SwitchDesktopWithAnimation(IVirtualDesktopModern desktop);
    [PreserveSig] int GetLastActiveDesktop(out IVirtualDesktopModern desktop);
    [PreserveSig] int WaitForAnimationToComplete();
}

[ComImport, Guid("4970BA3D-FD4E-4647-BEA3-D89076EF4B9C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManager22631
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktopModern desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(out IVirtualDesktopModern desktop);
    [PreserveSig] int GetDesktops(out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktopModern from, uint direction, out IVirtualDesktopModern adjacent);
    [PreserveSig] int SwitchDesktop(IVirtualDesktopModern desktop);
    [PreserveSig] int CreateDesktopW(out IVirtualDesktopModern desktop);
    [PreserveSig] int MoveDesktop(IVirtualDesktopModern desktop, uint index);
    [PreserveSig] int RemoveDesktop(IVirtualDesktopModern destroy, IVirtualDesktopModern fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktopModern desktop);
    [PreserveSig] int GetDesktopSwitchIncludeExcludeViews(IVirtualDesktopModern desktop, out IObjectArray a, out IObjectArray b);
    [PreserveSig] int SetName(IVirtualDesktopModern desktop, IntPtr hName);
    [PreserveSig] int SetWallpaper(IVirtualDesktopModern desktop, IntPtr hPath);
    [PreserveSig] int SetWallpaperForAllDesktops(IntPtr hPath);
    [PreserveSig] int CopyDesktopState(IntPtr view0, IntPtr view1);
    [PreserveSig] int CreateRemoteDesktop(IntPtr hStr, out IVirtualDesktopModern desktop);
    [PreserveSig] int pDesktop(IVirtualDesktopModern desktop);
    [PreserveSig] int SwitchRemoteDesktop(IVirtualDesktopModern desktop, uint unknown);
    [PreserveSig] int SwitchDesktopWithAnimation(IVirtualDesktopModern desktop);
    [PreserveSig] int GetLastActiveDesktop(out IVirtualDesktopModern desktop);
    [PreserveSig] int WaitForAnimationToComplete();
}

[ComImport, Guid("A3175F2D-239C-4BD2-8AA0-EEBA8B0B138E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManager22621
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktopModern desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(out IVirtualDesktopModern desktop);
    [PreserveSig] int GetDesktops(out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktopModern from, uint direction, out IVirtualDesktopModern adjacent);
    [PreserveSig] int SwitchDesktop(IVirtualDesktopModern desktop);
    [PreserveSig] int CreateDesktopW(out IVirtualDesktopModern desktop);
    [PreserveSig] int MoveDesktop(IVirtualDesktopModern desktop, IntPtr hwnd, int index);
    [PreserveSig] int RemoveDesktop(IVirtualDesktopModern destroy, IVirtualDesktopModern fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktopModern desktop);
    [PreserveSig] int Unknown(IVirtualDesktopModern desktop, out IObjectArray a, out IObjectArray b);
    [PreserveSig] int SetName(IVirtualDesktopModern desktop, IntPtr hName);
    [PreserveSig] int SetWallpaper(IVirtualDesktopModern desktop, IntPtr hPath);
    [PreserveSig] int SetWallpaperForAllDesktops(IntPtr hPath);
    [PreserveSig] int CopyDesktopState(IntPtr view0, IntPtr view1);
    [PreserveSig] int GetDesktopPerMonitor(out int state);
    [PreserveSig] int SetDesktopPerMonitor(int state);
}

[ComImport, Guid("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManager21313Family
{
    [PreserveSig] int GetCount(IntPtr hwnd, out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktop21313 desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(IntPtr hwnd, out IVirtualDesktop21313 desktop);
    [PreserveSig] int GetDesktops(IntPtr hwnd, out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktop21313 from, uint direction, out IVirtualDesktop21313 adjacent);
    [PreserveSig] int SwitchDesktop(IntPtr hwnd, IVirtualDesktop21313 desktop);
    [PreserveSig] int CreateDesktopW(IntPtr hwnd, out IVirtualDesktop21313 desktop);
    [PreserveSig] int MoveDesktop(IVirtualDesktop21313 desktop, IntPtr hwnd, int index);
    [PreserveSig] int RemoveDesktop(IVirtualDesktop21313 destroy, IVirtualDesktop21313 fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktop21313 desktop);
    [PreserveSig] int Unknown(IVirtualDesktop21313 desktop, out IObjectArray a, out IObjectArray b);
    [PreserveSig] int SetName(IVirtualDesktop21313 desktop, IntPtr hName);
    [PreserveSig] int SetWallpaper(IVirtualDesktop21313 desktop, IntPtr hPath);
    [PreserveSig] int SetWallpaperForAllDesktops(IntPtr hPath);
    [PreserveSig] int CopyDesktopState(IntPtr view0, IntPtr view1);
    [PreserveSig] int GetDesktopPerMonitor(out int state);
    [PreserveSig] int SetDesktopPerMonitor(int state);
}

[ComImport, Guid("094AFE11-44F2-4BA0-976F-29A97E263EE0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManager20231
{
    [PreserveSig] int GetCount(IntPtr hwnd, out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktop20231 desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(IntPtr hwnd, out IVirtualDesktop20231 desktop);
    [PreserveSig] int GetDesktops(IntPtr hwnd, out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktop20231 from, uint direction, out IVirtualDesktop20231 adjacent);
    [PreserveSig] int SwitchDesktop(IntPtr hwnd, IVirtualDesktop20231 desktop);
    [PreserveSig] int CreateDesktopW(IntPtr hwnd, out IVirtualDesktop20231 desktop);
    [PreserveSig] int RemoveDesktop(IVirtualDesktop20231 destroy, IVirtualDesktop20231 fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktop20231 desktop);
}

[ComImport, Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManagerWin10
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktopWin10 desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(out IVirtualDesktopWin10 desktop);
    [PreserveSig] int GetDesktops(out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktopWin10 from, uint direction, out IVirtualDesktopWin10 adjacent);
    [PreserveSig] int SwitchDesktop(IVirtualDesktopWin10 desktop);
    [PreserveSig] int CreateDesktopW(out IVirtualDesktopWin10 desktop);
    [PreserveSig] int RemoveDesktop(IVirtualDesktopWin10 destroy, IVirtualDesktopWin10 fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktopWin10 desktop);
}

[ComImport, Guid("0F3A72B0-4566-487E-9A33-4ED302F6D6CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IManagerInternal2
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int MoveViewToDesktop(IntPtr view, IVirtualDesktopWin10 desktop);
    [PreserveSig] int CanViewMoveDesktops(IntPtr view, out uint canMove);
    [PreserveSig] int GetCurrentDesktop(out IVirtualDesktopWin10 desktop);
    [PreserveSig] int GetDesktops(out IObjectArray array);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktopWin10 from, uint direction, out IVirtualDesktopWin10 adjacent);
    [PreserveSig] int SwitchDesktop(IVirtualDesktopWin10 desktop);
    [PreserveSig] int CreateDesktopW(out IVirtualDesktopWin10 desktop);
    [PreserveSig] int RemoveDesktop(IVirtualDesktopWin10 destroy, IVirtualDesktopWin10 fallback);
    [PreserveSig] int FindDesktop(ref Guid guid, out IVirtualDesktopWin10 desktop);
    [PreserveSig] int Unknown(IVirtualDesktopWin10 desktop, out IObjectArray a, out IObjectArray b);
    [PreserveSig] int SetName(IVirtualDesktopWin10 desktop, IntPtr hName);
}

// ---- Notification service + sink interfaces (YASB _NOTIFICATION_* order) ----

[ComImport, Guid("0CD45E71-D927-4F15-8B0A-8FEF525337BF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopNotificationService
{
    [PreserveSig] int Register(IntPtr sink, out uint cookie);
    [PreserveSig] int Unregister(uint cookie);
}

[ComImport, Guid("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotification23H2A
{
    [PreserveSig] int VirtualDesktopCreated(IntPtr p0);
    [PreserveSig] int VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int VirtualDesktopRenamed(IntPtr p0, IntPtr p1);
    [PreserveSig] int ViewVirtualDesktopChanged(IntPtr p0);
    [PreserveSig] int CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopSwitched(IntPtr p0);
    [PreserveSig] int RemoteVirtualDesktopConnected(IntPtr p0);
}

[ComImport, Guid("B287FA1C-7771-471A-A2DF-9B6B21F0D675"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotification23H2B
{
    [PreserveSig] int VirtualDesktopCreated(IntPtr p0);
    [PreserveSig] int VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int VirtualDesktopRenamed(IntPtr p0, IntPtr p1);
    [PreserveSig] int ViewVirtualDesktopChanged(IntPtr p0);
    [PreserveSig] int CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopSwitched(IntPtr p0);
    [PreserveSig] int RemoteVirtualDesktopConnected(IntPtr p0);
}

[ComImport, Guid("CD403E52-DEED-4C13-B437-B98380F2B1E8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotification21H2
{
    [PreserveSig] int VirtualDesktopCreated(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int VirtualDesktopDestroyed(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int Proc7(IntPtr p0);
    [PreserveSig] int VirtualDesktopMoved(IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3);
    [PreserveSig] int VirtualDesktopRenamed(IntPtr p0, IntPtr p1);
    [PreserveSig] int ViewVirtualDesktopChanged(IntPtr p0);
    [PreserveSig] int CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1, IntPtr p2);
    [PreserveSig] int VirtualDesktopWallpaperChanged(IntPtr p0, IntPtr p1);
}

[ComImport, Guid("C179334C-4295-40D3-BEA1-C654D965605A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotificationWin10
{
    [PreserveSig] int VirtualDesktopCreated(IntPtr p0);
    [PreserveSig] int VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyed(IntPtr p0, IntPtr p1);
    [PreserveSig] int ViewVirtualDesktopChanged(IntPtr p0);
    [PreserveSig] int CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1);
}

[ComImport, Guid("1BA7CF30-3591-43FA-ABFA-4AAF7ABEEDB7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotification2
{
    [PreserveSig] int VirtualDesktopCreated(IntPtr p0);
    [PreserveSig] int VirtualDesktopDestroyBegin(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyFailed(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopDestroyed(IntPtr p0, IntPtr p1);
    [PreserveSig] int ViewVirtualDesktopChanged(IntPtr p0);
    [PreserveSig] int CurrentVirtualDesktopChanged(IntPtr p0, IntPtr p1);
    [PreserveSig] int VirtualDesktopRenamed(IntPtr p0, IntPtr p1);
}
