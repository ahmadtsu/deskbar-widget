// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using AutomationCondition = System.Windows.Automation.Condition;

namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Measures the Windows 11 taskbar through UI Automation. Element names and tree
/// shape vary between Windows builds (see reference).
///
/// Contract (same as reference): measuring walks the automation tree and is far
/// too slow for the UI thread. Call <see cref="Measure"/> from a background thread
/// via Task.Run and marshal the result back; UIA reads are fine off the UI thread.
/// </summary>
internal sealed class TaskbarLayoutProbe
{
    // Taskbar children wider than this much of the bar are containers spanning it, not icons.
    private const double MaxIconClusterChildWidthRatio = 0.6;

    private const string TaskbarFrameAutomationId = "TaskbarFrame";
    private const string SystemTrayIconAutomationId = "SystemTrayIcon";
    private const string WidgetsButtonAutomationId = "WidgetsButton";

    private nint _cachedHandle;
    private AutomationElement? _cachedFrame;
    private AutomationElement[]? _cachedTrayIcons;

    /// <summary>
    /// Discards all cached AutomationElement references so stale bounding boxes
    /// are not reused across display or resolution changes.
    /// </summary>
    public void Invalidate()
    {
        _cachedFrame = null;
        _cachedTrayIcons = null;
        _cachedHandle = nint.Zero;
    }

    /// <param name="refreshElements">
    /// False re-reads the positions of the elements found last time instead of searching for them
    /// again. Valid while a reflow is settling, where things move but none come or go.
    /// </param>
    public TaskbarLayout Measure(nint taskbarHandle, bool refreshElements)
    {
        var obstacles = new List<Rect>();
        Rect? iconCluster = null;

        if (taskbarHandle != _cachedHandle || refreshElements)
        {
            Invalidate();
        }

        try
        {
            var taskbar = AutomationElement.FromHandle(taskbarHandle);
            if (taskbar is null)
            {
                return new TaskbarLayout(null, obstacles);
            }

            foreach (var icon in GetTrayIcons(taskbarHandle, taskbar, refreshElements))
            {
                try
                {
                    var iconRect = icon.Current.BoundingRectangle;
                    if (iconRect.Width > 0 && iconRect.Height > 0)
                    {
                        obstacles.Add(iconRect);
                    }
                }
                catch
                {
                    // Stale icon reference during reflow
                }
            }

            var frame = GetFrame(taskbarHandle, taskbar, refreshElements);
            if (frame != null)
            {
                var taskbarRect = taskbar.Current.BoundingRectangle;
                bool isVertical = taskbarRect.Height > taskbarRect.Width;
                double maxIconDimension =
                    (isVertical ? taskbarRect.Height : taskbarRect.Width) * MaxIconClusterChildWidthRatio;

                foreach (
                    AutomationElement child in frame.FindAll(TreeScope.Children, AutomationCondition.TrueCondition)
                )
                {
                    try
                    {
                        var rect = child.Current.BoundingRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0)
                        {
                            continue;
                        }

                        if (child.Current.AutomationId is SystemTrayIconAutomationId or WidgetsButtonAutomationId)
                        {
                            obstacles.Add(rect);
                        }
                        else if ((isVertical ? rect.Height : rect.Width) <= maxIconDimension)
                        {
                            iconCluster = iconCluster.HasValue ? Rect.Union(iconCluster.Value, rect) : rect;
                        }
                    }
                    catch
                    {
                        // Stale child element during desktop switch or window animation
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Most likely a cached element that went away with its taskbar; resolve them again next time.
            _cachedFrame = null;
            _cachedTrayIcons = null;
            Debug.WriteLine($"TaskbarLayoutProbe: could not measure the taskbar layout: {ex.Message}");
        }

        return new TaskbarLayout(iconCluster, obstacles);
    }

    /// <summary>
    /// Finding the frame is a full descendant walk, and it outlives every reflow, so it is resolved
    /// once per taskbar rather than on each measurement, unless explicitly refreshed.
    /// </summary>
    private AutomationElement? GetFrame(nint taskbarHandle, AutomationElement taskbar, bool refresh)
    {
        if (refresh || _cachedFrame == null || _cachedHandle != taskbarHandle)
        {
            _cachedFrame = taskbar.FindFirst(TreeScope.Descendants, ById(TaskbarFrameAutomationId));
            _cachedHandle = taskbarHandle;
        }

        return _cachedFrame;
    }

    /// <summary>
    /// Depending on the Windows build the tray icons sit outside the taskbar frame, so they are
    /// searched from the root rather than among the frame's children — the most expensive part of a
    /// measurement, and the tray does not move while task buttons reflow.
    /// </summary>
    private AutomationElement[] GetTrayIcons(nint taskbarHandle, AutomationElement taskbar, bool refresh)
    {
        if (refresh || _cachedTrayIcons == null || _cachedHandle != taskbarHandle)
        {
            _cachedTrayIcons = taskbar
                .FindAll(TreeScope.Descendants, ById(SystemTrayIconAutomationId))
                .Cast<AutomationElement>()
                .ToArray();
            _cachedHandle = taskbarHandle;
        }

        return _cachedTrayIcons;
    }

    private static PropertyCondition ById(string automationId) =>
        new(AutomationElement.AutomationIdProperty, automationId);
}
