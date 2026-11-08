using DeskbarWidget.App;
using DeskbarWidget.Core;
using DeskbarWidget.Interop;
using Xunit;

namespace DeskbarWidget.Tests;

public sealed class DesktopsStripTests
{
    private static readonly Guid G1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid G2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid G3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static FakeReader ThreeDesktops() => new()
    {
        Desktops =
        [
            new DesktopData(1, G1, "One"),
            new DesktopData(2, G2, "Two"),
            new DesktopData(3, G3, "Three"),
        ],
        CurrentGuid = G1,
    };

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    [Fact]
    public void DesktopsStrip_WhenHideLockedDesktopsFalse_ShowsAllDesktops()
    {
        RunInSta(() =>
        {
            var fake = ThreeDesktops();
            using var svc = new VirtualDesktopService(fake);
            svc.LockDesktop(2);

            var strip = new DesktopsStrip();
            strip.Attach(svc, new DeskbarWidget.App.Themes.ThemeWatcher(), showAllNames: false, hideLockedDesktops: false);

            Assert.Equal(3, strip.TileCount);
            Assert.False(strip.HideLockedDesktops);
        });
    }

    [Fact]
    public void DesktopsStrip_WhenHideLockedDesktopsTrue_HidesLockedDesktops()
    {
        RunInSta(() =>
        {
            var fake = ThreeDesktops();
            using var svc = new VirtualDesktopService(fake);
            svc.LockDesktop(2);

            var strip = new DesktopsStrip();
            strip.Attach(svc, new DeskbarWidget.App.Themes.ThemeWatcher(), showAllNames: false, hideLockedDesktops: true);

            Assert.Equal(2, strip.TileCount);
            Assert.True(strip.HideLockedDesktops);

            // Toggle to false -> all 3 reappear
            strip.SetHideLockedDesktops(false);
            Assert.Equal(3, strip.TileCount);
            Assert.False(strip.HideLockedDesktops);

            // Toggle back to true -> locked is hidden again
            strip.SetHideLockedDesktops(true);
            Assert.Equal(2, strip.TileCount);
            Assert.True(strip.HideLockedDesktops);
        });
    }
}
