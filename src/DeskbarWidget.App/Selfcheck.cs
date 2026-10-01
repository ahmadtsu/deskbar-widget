// Diagnostic runner inspired by YASB `selfcheck.py` (MIT © 2024 amnweb):
// prints build, COM tier, desktops + current without touching anything.
// Exit code 0 = all checks pass, 1 = any failure.

using System.IO;
using System.Runtime.InteropServices;
using DeskbarWidget.Core;
using DeskbarWidget.Interop;

namespace DeskbarWidget.App;

internal static class Selfcheck
{
    public static int Run(bool full)
    {
        var failures = 0;
        void Check(string name, Func<string?> fn)
        {
            try
            {
                var detail = fn();
                Console.WriteLine($"  OK   {name}" + (detail is null ? "" : $"  {detail}"));
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL {name}  {ex.GetType().Name}: {ex.Message}");
                for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                {
                    Console.WriteLine($"         <- {inner.GetType().Name}: {inner.Message}");
                }
            }
        }

        var build = Environment.OSVersion.Version.Build;
        Console.WriteLine($"Windows build {build}");
        Console.WriteLine($".NET {Environment.Version}");

        int tier = -1;
        Check("resolve tier", () =>
        {
            tier = new TierResolver().Resolve();
            return $"tier {tier}";
        });

        var api = new VirtualDesktopApi();
        Check("acquire COM interfaces", () => api.Available() ? "usable" : throw new VirtualDesktopException("unavailable"));
        Check("supports desktop names", () => api.SupportsNames.ToString());
        Check("supports per-desktop wallpaper", () => api.SupportsWallpaper.ToString());
        Check("autostart registered", () =>
        {
            var autostart = new AutostartService();
            return autostart.CheckIsEnabled() ? "enabled" : "disabled";
        });
        Check("configuration file", () =>
        {
            var configPath = ConfigService.Instance.ConfigFilePath;
            return File.Exists(configPath) ? $"exists ({configPath})" : $"defaults active ({configPath})";
        });

        List<DesktopInfo>? desktops = null;
        Check("list desktops", () =>
        {
            using var svc = new VirtualDesktopService();
            desktops = svc.GetDesktops().ToList();
            return $"{desktops.Count} desktop(s)";
        });

        Check("read current desktop", () =>
        {
            using var svc = new VirtualDesktopService();
            var current = svc.GetCurrentDesktop();
            return $"#{current.Number} {current.DisplayName}";
        });

        if (desktops is { Count: > 0 })
        {
            Console.WriteLine("\ndesktops:");
            using var svc = new VirtualDesktopService();
            var current = svc.GetCurrentDesktop();
            foreach (var d in desktops)
            {
                var mark = d.Guid == current.Guid ? "  <- current" : "";
                Console.WriteLine($"   {d.Number}. name=\"{d.Name}\" {d.Guid}{mark}");
            }
        }

        if (full)
        {
            Console.WriteLine("\n(full mode: create/switch/remove not yet implemented in v1)");
        }

        Console.WriteLine(failures == 0 ? "\nAll checks passed." : $"\n{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
