// Translated from YASB Reborn `interfaces.py` tier logic (MIT © 2024 amnweb).
// See ATTRIBUTION.md. Each tier is the build its interface set first appeared in.

namespace DeskbarWidget.Interop;

internal static class VirtualDesktopTiers
{
    public const int Win10 = 0;
    public const int V20231 = 20231;
    public const int V21313 = 21313;
    public const int V22449 = 22449;
    public const int V22621 = 22621;
    public const int V22631 = 22631;
    public const int V26100 = 26100;

    // Genuine OS capability gates (not vtable tiers).
    public const int BuildDesktopNames = 19041;

    public static readonly IReadOnlyDictionary<int, Guid> ManagerGuidByTier = new Dictionary<int, Guid>
    {
        [V26100] = VirtualDesktopGuids.IID_Manager_26100,
        [V22631] = VirtualDesktopGuids.IID_Manager_22631,
        [V22621] = VirtualDesktopGuids.IID_Manager_22621,
        // 22449 shares the 21313 GUID; distinguished by build number after a
        // successful probe (mirrors YASB).
        [V22449] = VirtualDesktopGuids.IID_Manager_21313,
        [V21313] = VirtualDesktopGuids.IID_Manager_21313,
        [V20231] = VirtualDesktopGuids.IID_Manager_20231,
        [Win10] = VirtualDesktopGuids.IID_Manager_9000,
    };

    // Newest first. 22449 absent (shares 21313's GUID).
    public static readonly int[] ProbeOrder = [V22631, V22621, V21313, V20231, Win10];

    public static bool SupportsNames(int tier, int build)
    {
        if (tier >= V20231)
        {
            return true;
        }

        return build >= BuildDesktopNames;
    }

    public static bool SupportsWallpaper(int tier) => tier >= V21313;
}
