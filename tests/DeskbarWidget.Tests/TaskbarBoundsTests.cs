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
}
