// Translated from YASB Reborn (MIT © 2024 amnweb; original Yasb MIT © 2021 denBot).
// See ATTRIBUTION.md. COM GUIDs for the undocumented Windows virtual desktop API.
// GUID values after https://github.com/Ciantic/VirtualDesktopAccessor (via YASB).

namespace DeskbarWidget.Interop;

internal static class VirtualDesktopGuids
{
    // Service/class IDs (stable across builds).
    public static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    public static readonly Guid CLSID_VirtualDesktopManagerInternalService = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    public static readonly Guid CLSID_VirtualDesktopPinnedApps = new("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    public static readonly Guid CLSID_VirtualDesktopNotificationService = new("A501FDEC-4A09-464C-AE4E-1B9C21B84918");

    public static readonly Guid IID_IServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    public static readonly Guid IID_IObjectArray = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");

    // Per-tier IVirtualDesktop IIDs.
    public static readonly Guid IID_IVirtualDesktop_26100 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");
    public static readonly Guid IID_IVirtualDesktop_22631 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");
    public static readonly Guid IID_IVirtualDesktop_22621 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");
    public static readonly Guid IID_IVirtualDesktop_21313 = new("536D3495-B208-4CC9-AE26-DE8111275BF8");
    public static readonly Guid IID_IVirtualDesktop_20231 = new("62FDF88B-11CA-4AFB-8BD8-2296DFAE49E2");
    public static readonly Guid IID_IVirtualDesktop_9000 = new("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4");

    // Per-tier IVirtualDesktopManagerInternal IIDs.
    public static readonly Guid IID_Manager_26100 = new("53F5CA0B-158F-4124-900C-057158060B27");
    public static readonly Guid IID_Manager_22631 = new("4970BA3D-FD4E-4647-BEA3-D89076EF4B9C");
    public static readonly Guid IID_Manager_22621 = new("A3175F2D-239C-4BD2-8AA0-EEBA8B0B138E");
    public static readonly Guid IID_Manager_21313 = new("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10");
    public static readonly Guid IID_Manager_20231 = new("094AFE11-44F2-4BA0-976F-29A97E263EE0");
    public static readonly Guid IID_Manager_9000 = new("F31574D6-B682-4CDC-BD56-1827860ABEC6");

    public static readonly Guid IID_IVirtualDesktopManagerInternal2 = new("0F3A72B0-4566-487E-9A33-4ED302F6D6CE");
    public static readonly Guid IID_IVirtualDesktop2 = new("31EBDE3F-6EC3-4CBD-B9FB-0EF6D09B41F4");

    // Notification sink IIDs.
    public static readonly Guid IID_Notification_22631 = new("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE");
    public static readonly Guid IID_Notification_22621 = new("B287FA1C-7771-471A-A2DF-9B6B21F0D675");
    public static readonly Guid IID_Notification_20231 = new("CD403E52-DEED-4C13-B437-B98380F2B1E8");
    public static readonly Guid IID_Notification_9000 = new("C179334C-4295-40D3-BEA1-C654D965605A");
    public static readonly Guid IID_Notification2 = new("1BA7CF30-3591-43FA-ABFA-4AAF7ABEEDB7");

    // Stable notification service IID.
    public static readonly Guid IID_IVirtualDesktopNotificationService = new("0CD45E71-D927-4F15-8B0A-8FEF525337BF");
}
