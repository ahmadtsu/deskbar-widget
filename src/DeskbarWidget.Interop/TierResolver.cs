// Translated from YASB `interfaces.resolve_tier` (MIT © 2024 amnweb).
// Only the Win11 range is ambiguous (Moment updates changed shells without a
// build change), so only that range probes the shell directly.

using System.Runtime.InteropServices;

namespace DeskbarWidget.Interop;

public sealed class TierResolver
{
    private readonly Func<IServiceProvider> _providerFactory;

    public TierResolver(Func<IServiceProvider>? providerFactory = null)
    {
        _providerFactory = providerFactory ?? CreateProvider;
    }

    public int Resolve()
    {
        var build = Environment.OSVersion.Version.Build;
        if (build >= VirtualDesktopTiers.V26100)
        {
            return VirtualDesktopTiers.V26100;
        }

        if (build < VirtualDesktopTiers.V20231)
        {
            return VirtualDesktopTiers.Win10;
        }

        var provider = _providerFactory();
        try
        {
            foreach (var tier in VirtualDesktopTiers.ProbeOrder)
            {
                var iid = VirtualDesktopTiers.ManagerGuidByTier[tier];
                if (ManagerAvailable(provider, iid))
                {
                    if (tier == VirtualDesktopTiers.V21313 && build >= VirtualDesktopTiers.V22449)
                    {
                        return VirtualDesktopTiers.V22449;
                    }

                    return tier;
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(provider);
        }

        throw new VirtualDesktopUnsupportedException(
            $"No supported IVirtualDesktopManagerInternal on build {build}.");
    }

    internal static bool ManagerAvailable(IServiceProvider provider, Guid iid)
    {
        var service = VirtualDesktopGuids.CLSID_VirtualDesktopManagerInternalService;
        var iidCopy = iid;
        try
        {
            var hr = provider.QueryService(ref service, ref iidCopy, out var obj);
            if (hr == 0 && obj != IntPtr.Zero)
            {
                Marshal.Release(obj);
                return true;
            }

            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static IServiceProvider CreateProvider()
    {
        var clsid = VirtualDesktopGuids.CLSID_ImmersiveShell;
        var iid = VirtualDesktopGuids.IID_IServiceProvider;
        var hr = NativeCom.CoCreateInstance(
            ref clsid, IntPtr.Zero, NativeCom.CLSCTX_LOCAL_SERVER, ref iid, out var obj);
        if (hr != 0 || obj == IntPtr.Zero)
        {
            throw new VirtualDesktopUnsupportedException(
                $"ImmersiveShell unavailable (HRESULT 0x{hr:X8}).");
        }

        return (IServiceProvider)Marshal.GetObjectForIUnknown(obj);
    }
}
