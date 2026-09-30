// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Window bounds in taskbar-client pixels (as placed via SetWindowPos).
/// </summary>
internal readonly record struct WidgetBounds(int Left, int Top, int Width, int Height)
{
    public static WidgetBounds Lerp(WidgetBounds from, WidgetBounds to, double progress) =>
        new(
            Interpolate(from.Left, to.Left, progress),
            Interpolate(from.Top, to.Top, progress),
            Interpolate(from.Width, to.Width, progress),
            Interpolate(from.Height, to.Height, progress)
        );

    private static int Interpolate(int from, int to, double progress) =>
        (int)Math.Round(from + (to - from) * progress);
}
