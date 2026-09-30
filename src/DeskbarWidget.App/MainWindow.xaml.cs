using System.Windows;
using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// Temporary Phase-1 window: lists desktops + current. Replaced by the
/// taskbar-docked squircle strip in Phase 2/3.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        try
        {
            using var svc = new VirtualDesktopService();
            var desktops = svc.GetDesktops();
            var current = svc.GetCurrentDesktop();
            Title = $"DeskbarWidget — {desktops.Count} desktop(s), current #{current.Number}";
        }
        catch (Exception ex)
        {
            Title = $"DeskbarWidget — unavailable: {ex.Message}";
        }
    }
}