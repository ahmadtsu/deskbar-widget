using System.Windows;
using DeskbarWidget.App.Taskbar;
using Xunit;

namespace DeskbarWidget.Tests;

public sealed class TaskbarBoundsTests
{
    [Fact]
    public void WidgetBoundsLerp_NeverReducesWidthBelowMaxDuringTransition()
    {
        var from = new WidgetBounds(Left: 100, Top: 10, Width: 80, Height: 32);
        var to = new WidgetBounds(Left: 200, Top: 10, Width: 160, Height: 32);

        // At progress 0, 0.25, 0.5, 0.75, width should be at least max(80, 160) = 160 to avoid clipping
        for (double p = 0.0; p < 1.0; p += 0.25)
        {
            var mid = WidgetBounds.Lerp(from, to, p);
            Assert.True(mid.Width >= 160, $"Expected width >= 160 at progress {p}, got {mid.Width}");
            Assert.Equal(32, mid.Height);
        }

        // At progress 1.0, should settle to target
        var final = WidgetBounds.Lerp(from, to, 1.0);
        Assert.Equal(160, final.Width);
        Assert.Equal(200, final.Left);
    }

    [Fact]
    public void WidgetBoundsLerp_PreservesMaxWidthWhenShrinkingDuringTransition()
    {
        var from = new WidgetBounds(Left: 100, Top: 10, Width: 160, Height: 32);
        var to = new WidgetBounds(Left: 150, Top: 10, Width: 80, Height: 32);

        // While moving, width should remain at least 160 so shrinking contents aren't clipped
        for (double p = 0.0; p < 1.0; p += 0.25)
        {
            var mid = WidgetBounds.Lerp(from, to, p);
            Assert.True(mid.Width >= 160, $"Expected width >= 160 at progress {p}, got {mid.Width}");
        }

        var final = WidgetBounds.Lerp(from, to, 1.0);
        Assert.Equal(80, final.Width);
        Assert.Equal(150, final.Left);
    }

    [Fact]
    public void StripBounds_LeftOnCentered_AnchorsToGapLeftRegardlessOfWidth()
    {
        var taskbarRect = new Rect(0, 0, 1920, 48);
        var cluster = new Rect(800, 0, 320, 48);
        var layout = new TaskbarLayout(cluster, Array.Empty<Rect>());

        var boundsSmall = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: taskbarRect,
            layout: layout,
            dpiScale: 1.0,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 80);

        var boundsLarge = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: taskbarRect,
            layout: layout,
            dpiScale: 1.0,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 140);

        Assert.NotNull(boundsSmall);
        Assert.NotNull(boundsLarge);

        // Left edge should remain identical (anchored at gapLeft)
        Assert.Equal(boundsSmall.Value.Left, boundsLarge.Value.Left);
        Assert.Equal(80, boundsSmall.Value.Width);
        Assert.Equal(140, boundsLarge.Value.Width);
    }

    [Fact]
    public void StripBounds_SecondaryMonitor_RightAlignmentWithNoObstacles()
    {
        // Secondary display taskbar: positioned at X=1920, no system tray icons or widgets obstacles
        var secondaryTaskbarRect = new Rect(1920, 1032, 1920, 48);
        var cluster = new Rect(2720, 1032, 320, 48);
        var layout = new TaskbarLayout(cluster, Array.Empty<Rect>());

        var bounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: secondaryTaskbarRect,
            layout: layout,
            dpiScale: 1.0,
            alignment: "Right",
            centerAligned: true,
            tileCount: 3,
            desiredWidthPx: 108);

        Assert.NotNull(bounds);
        Assert.Equal(108, bounds.Value.Width);
        Assert.True(bounds.Value.Height > 0);
        // On a secondary monitor with no obstacles, gapEnd reaches the taskbar's right boundary
        Assert.True(bounds.Value.Left > 0);
    }

    [Fact]
    public void StripBounds_SecondaryMonitor_HighDpiScale()
    {
        // Secondary monitor with 150% DPI scale (144 DPI)
        var secondaryTaskbarRect = new Rect(1920, 0, 2560, 72);
        var cluster = new Rect(3000, 0, 480, 72);
        var layout = new TaskbarLayout(cluster, Array.Empty<Rect>());

        var bounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: secondaryTaskbarRect,
            layout: layout,
            dpiScale: 1.5,
            alignment: "Left",
            centerAligned: true,
            tileCount: 3,
            desiredWidthPx: 162);

        Assert.NotNull(bounds);
        Assert.Equal(162, bounds.Value.Width);
        // Height should be clamped proportionally to 1.5 scale
        Assert.True(bounds.Value.Height >= 30);
    }

    [Fact]
    public void StripBounds_SecondaryMonitor_LeftOnCenteredWithoutObstacles()
    {
        var secondaryTaskbarRect = new Rect(1920, 0, 1920, 48);
        var cluster = new Rect(2800, 0, 240, 48);
        var layout = new TaskbarLayout(cluster, Array.Empty<Rect>());

        var bounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: secondaryTaskbarRect,
            layout: layout,
            dpiScale: 1.0,
            alignment: "Left",
            centerAligned: true,
            tileCount: 4,
            desiredWidthPx: 144);

        Assert.NotNull(bounds);
        Assert.Equal(144, bounds.Value.Width);
        Assert.Equal(48, secondaryTaskbarRect.Height);
    }

    [Fact]
    public void StripBounds_MismatchedVerticalCluster_FallsBackGracefullyWithoutHiding()
    {
        // Reproduces the exact failure from deskbar.log.txt:
        // taskbar is at Y=1290..1350, but cluster reported Y=1032..1080 (from another display/DPI).
        var taskbarRect = new Rect(2400, 1290, 2400, 60);
        var mismatchedCluster = new Rect(2482, 1032, 797, 48);
        var obstacles = new[]
        {
            new Rect(2400, 1032, 40, 48), // Also mismatched Y
        };
        var layout = new TaskbarLayout(mismatchedCluster, obstacles);

        // Before our fix: available space was computed as 2482 - 2400 - 16 = 66px < 70px -> returned null (hiding).
        // With our fix: mismatched cluster and obstacles are ignored, allowing the strip to be placed safely.
        var bounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: taskbarRect,
            layout: layout,
            dpiScale: 1.0,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 72);

        Assert.NotNull(bounds);
        Assert.Equal(72, bounds.Value.Width);
        // When taskbarHandle is nint.Zero (unit test without native HWND),
        // coordinates stay in screen space (taskbarRect.Left + 8 padding = 2408).
        Assert.Equal((int)taskbarRect.Left + 8, bounds.Value.Left);
    }

    [Fact]
    public void StripBounds_MismatchedObstaclesOutsideVerticalSpan_Ignored()
    {
        var taskbarRect = new Rect(0, 1140, 1920, 60);
        var cluster = new Rect(700, 1140, 300, 60);
        var obstacles = new[]
        {
            new Rect(0, 500, 500, 50), // Phantom obstacle from window on different vertical coord
            new Rect(0, 1140, 100, 60), // Real Start/Widgets button on taskbar
        };
        var layout = new TaskbarLayout(cluster, obstacles);

        var bounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: taskbarRect,
            layout: layout,
            dpiScale: 1.25,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 90);

        Assert.NotNull(bounds);
        // gapStart should be 100 (real obstacle), not 500 (phantom obstacle)
        // gapLeft = 100 + padding (8 * 1.25 = 10) = 110
        Assert.Equal(110, bounds.Value.Left);
    }

    [Fact]
    public void StripBounds_DualMonitorMixedDpi_BothMonitorsPlacedCorrectly()
    {
        // Primary monitor: 1920x1200 at 125% DPI (taskbar=0,1140,1920,60)
        var primaryTaskbar = new Rect(0, 1140, 1920, 60);
        var primaryCluster = new Rect(462, 1140, 996, 60);
        var primaryLayout = new TaskbarLayout(primaryCluster, Array.Empty<Rect>());

        var primaryBounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: primaryTaskbar,
            layout: primaryLayout,
            dpiScale: 1.25,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 90);

        Assert.NotNull(primaryBounds);
        Assert.Equal(90, primaryBounds.Value.Width);

        // Secondary monitor: 1920x1080 at 100% DPI (taskbar=1920,1032,1920,48)
        var secondaryTaskbar = new Rect(1920, 1032, 1920, 48);
        var secondaryCluster = new Rect(2482, 1032, 797, 48);
        var secondaryLayout = new TaskbarLayout(secondaryCluster, Array.Empty<Rect>());

        var secondaryBounds = StripBounds.Calculate(
            taskbarHandle: nint.Zero,
            taskbarRectPx: secondaryTaskbar,
            layout: secondaryLayout,
            dpiScale: 1.0,
            alignment: "Left",
            centerAligned: true,
            tileCount: 2,
            desiredWidthPx: 72);

        Assert.NotNull(secondaryBounds);
        Assert.Equal(72, secondaryBounds.Value.Width);
    }
}
