// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
using System.Windows;

namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Pure geometry for our number-strip: fits it into the free stretch of taskbar
/// between the icon cluster and the nearest thing on the aligned side.
/// Returns null when the gap is too narrow (caller hides) or the taskbar is
/// vertical (unsupported in v1; caller falls back to a standalone bar).
/// All rects/layout in screen pixels; returned bounds in taskbar-client pixels.
/// </summary>
internal static class StripBounds
{
    // Baseline taskbar height (default Win11 taskbar). Margins and strip height scale
    // proportionally so the strip adapts when the taskbar is made slimmer.
    private const double SearchBoxVerticalMarginFraction = 0.125; // 12.5% of taskbar height
    private const double MinWidgetHeightAbsoluteDip = 20;
    private const double MaxWidgetHeightDip = 48;
    private const double HorizontalPaddingDip = 8;

    // Our strip: 28dip tiles with an 8dip gap (36dip pitch).
    private const double TileSizeDip = 28;
    private const double TileGapDip = 8;
    private const double TilePitchDip = TileSizeDip + TileGapDip;

    // Narrower than this nothing useful fits; caller hides instead of squeezing.
    private const double MinStripWidthDip = 70;

    /// <param name="taskbarHandle">Taskbar HWND, used for screen-to-client projection.</param>
    /// <param name="taskbarRectPx">Taskbar rect in screen pixels.</param>
    /// <param name="layout">Measured icon cluster + obstacles in screen pixels.</param>
    /// <param name="dpiScale">DPI scale (dpi / 96).</param>
    /// <param name="alignment">"Left" or "Right". "Left" only applies on a centered taskbar.</param>
    /// <param name="centerAligned">Whether the taskbar is center-aligned.</param>
    /// <param name="tileCount">Number of tiles; desired width is tileCount * 36dip * scale.</param>
    /// <param name="desiredWidthPx">Measured content width in px (0 = use tile-count formula).</param>
    public static WidgetBounds? Calculate(
        nint taskbarHandle,
        Rect taskbarRectPx,
        TaskbarLayout layout,
        double dpiScale,
        string alignment,
        bool centerAligned,
        int tileCount,
        double desiredWidthPx = 0)
    {
        if (tileCount <= 0 || dpiScale <= 0)
        {
            return null;
        }

        int taskbarWidth = (int)Math.Round(taskbarRectPx.Width);
        int taskbarHeight = (int)Math.Round(taskbarRectPx.Height);
        if (taskbarWidth <= 0 || taskbarHeight <= 0)
        {
            return null;
        }

        // Vertical taskbar: unsupported in v1, caller falls back to standalone bar.
        if (taskbarHeight > taskbarWidth)
        {
            return null;
        }

        // "Left" alignment is only offered on a centered taskbar, where the strip sits in the empty
        // area to the left of the centered cluster; every other case fills the space to its right.
        bool leftOnCentered =
            string.Equals(alignment, "Left", StringComparison.Ordinal) && centerAligned;

        // The gap runs from the cluster edge to the nearest obstacle beyond it, falling back to the
        // taskbar edge. An unmeasurable cluster widens it to the whole taskbar, which still lands the
        // strip in the aligned corner because it is capped at its desired width below.
        double gapStart;
        double gapEnd;

        if (leftOnCentered)
        {
            gapEnd = layout.IconCluster?.Left ?? taskbarRectPx.Right;
            gapStart = layout.Obstacles.Select(o => o.Right).Where(x => x <= gapEnd).Append(taskbarRectPx.Left).Max();
        }
        else
        {
            gapStart = layout.IconCluster?.Right ?? taskbarRectPx.Left;
            gapEnd = layout.Obstacles.Select(o => o.Left).Where(x => x >= gapStart).Append(taskbarRectPx.Right).Min();
        }

        int padding = (int)(HorizontalPaddingDip * dpiScale);
        var (gapLeft, gapRight) = ProjectSpan(taskbarHandle, gapStart, gapEnd, padding);
        int available = gapRight - gapLeft;

        if (available < (int)(MinStripWidthDip * dpiScale))
        {
            return null;
        }

        int height = CalculateStripHeight(taskbarHeight, dpiScale);

        // Width comes from the measured WPF content (DIPs -> px), not from a
        // tile-count formula, so the HWND can never clip the rendered tiles.
        // Falls back to the formula when content hasn't been measured yet.
        int desiredWidth = desiredWidthPx > 0
            ? Math.Min((int)Math.Ceiling(desiredWidthPx), available)
            : (int)Math.Round(tileCount * TilePitchDip * dpiScale);
        if (desiredWidth <= 0)
        {
            return null;
        }

        int width = Math.Min(available, desiredWidth);

        return new WidgetBounds(
            leftOnCentered ? gapLeft : gapRight - width,
            (taskbarHeight - height) / 2,
            width,
            height
        );
    }

    private static int CalculateStripHeight(int taskbarHeight, double dpiScale)
    {
        int margin = (int)(SearchBoxVerticalMarginFraction * taskbarHeight);
        int minHeight = (int)(MinWidgetHeightAbsoluteDip * dpiScale);
        int maxHeight = (int)(MaxWidgetHeightDip * dpiScale);
        return Math.Clamp(taskbarHeight - (2 * margin), minHeight, maxHeight);
    }

    private static (int Start, int End) ProjectSpan(
        nint taskbarHandle,
        double screenStart,
        double screenEnd,
        int padding)
    {
        int startX = (int)Math.Round(screenStart);
        int startY = 0;
        int endX = (int)Math.Round(screenEnd);
        int endY = 0;

        // Best-effort conversion; on failure the values stay as screen coords.
        NativeTaskbar.ScreenToClient(taskbarHandle, ref startX, ref startY);
        NativeTaskbar.ScreenToClient(taskbarHandle, ref endX, ref endY);

        int spanStart = Math.Min(startX, endX) + padding;
        int spanEnd = Math.Max(startX, endX) - padding;
        return (spanStart, spanEnd);
    }
}
