// Adapted from YASB Reborn WorkspaceButton.rename_desktop (MIT © 2024 amnweb).
// See ATTRIBUTION.md.
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeskbarWidget.App.Themes;
using DeskbarWidget.Core;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace DeskbarWidget.App;

internal partial class RenameWindow : Window
{
    private readonly VirtualDesktopService _service;
    private readonly ThemeWatcher _watcher;
    private readonly int _number;

    private bool _isClosing;

    public RenameWindow(VirtualDesktopService service, ThemeWatcher watcher, int number, string currentName)
    {
        _service = service;
        _watcher = watcher;
        _number = number;

        InitializeComponent();

        TileTheme.ApplyTo(this, _watcher.Current);

        NameInput.Text = currentName;
        NameInput.TextChanged += (_, _) =>
        {
            RenameBtn.IsEnabled = !string.IsNullOrWhiteSpace(NameInput.Text);
        };

        void OnThemeChanged(object? sender, ThemeSnapshot snapshot) =>
            Dispatcher.BeginInvoke(() => TileTheme.ApplyTo(this, snapshot));

        _watcher.Changed += OnThemeChanged;
        Closed += (_, _) => _watcher.Changed -= OnThemeChanged;
        Closing += (_, _) => _isClosing = true;

        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };

        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                SafeClose();
            }
        };
    }

    private void SafeClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        try
        {
            Close();
        }
        catch
        {
            // Ignore if window is already closed or closing
        }
    }

    public void ShowRelativeTo(FrameworkElement target)
    {
        PositionRelativeTo(target);
        Show();
        Activate();
    }

    private void PositionRelativeTo(FrameworkElement target)
    {
        var dpi = VisualTreeHelper.GetDpi(target);
        var screenPos = target.PointToScreen(new Point(0, 0));
        double targetLeftDip = screenPos.X / dpi.DpiScaleX;
        double targetTopDip = screenPos.Y / dpi.DpiScaleY;
        double targetWidthDip = target.ActualWidth;
        double targetHeightDip = target.ActualHeight;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double dialogWidth = DesiredSize.Width > 0 ? DesiredSize.Width : 344;
        double dialogHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 170;

        double left = targetLeftDip + (targetWidthDip / 2) - (dialogWidth / 2);

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)screenPos.X, (int)screenPos.Y));
        var workAreaPx = screen.WorkingArea;
        var workArea = new Rect(
            workAreaPx.Left / dpi.DpiScaleX,
            workAreaPx.Top / dpi.DpiScaleY,
            workAreaPx.Width / dpi.DpiScaleX,
            workAreaPx.Height / dpi.DpiScaleY);

        // Float above the taskbar tile; if off-screen, flip below.
        double top = targetTopDip - dialogHeight + 4;
        if (top < workArea.Top)
        {
            top = targetTopDip + targetHeightDip - 4;
        }

        if (left < workArea.Left)
        {
            left = workArea.Left + 8;
        }
        else if (left + dialogWidth > workArea.Right)
        {
            left = workArea.Right - dialogWidth - 8;
        }

        Left = left;
        Top = top;
    }

    private void OnRenameClicked(object sender, RoutedEventArgs e)
    {
        var newName = NameInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            try
            {
                DeskLogger.Info($"rename desktop #{_number} to '{newName}'");
                _service.RenameDesktop(_number, newName);
            }
            catch (Exception ex)
            {
                DeskLogger.Info($"rename desktop #{_number} failed: {ex.Message}");
            }
        }

        SafeClose();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        SafeClose();
    }
}
