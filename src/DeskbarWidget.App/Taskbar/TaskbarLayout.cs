// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
using System.Windows;

namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Icon-cluster bounds plus obstacles a strip placed on the taskbar must not
/// overlap, all in screen pixels (like the reference).
/// </summary>
internal readonly record struct TaskbarLayout(Rect? IconCluster, IReadOnlyList<Rect> Obstacles)
{
    public bool IsMeasurable => IconCluster.HasValue || Obstacles.Count > 0;
}
