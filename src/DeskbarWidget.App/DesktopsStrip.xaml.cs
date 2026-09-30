using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// Horizontal row of squircle tiles, YASB-style: inactive tiles show the
/// desktop number at fixed 28px; the active tile grows into an accent pill
/// showing the desktop name (label_workspace_active_btn behavior). Width
/// changes glide with a 200ms animation; the taskbar window re-measures
/// content so it always fits.
/// </summary>
internal partial class DesktopsStrip
{
    private const double InactiveWidth = 28;
    private const double MaxActiveWidth = 160;
    private const double ActiveHorizontalPadding = 20;

    private static readonly Duration GrowDuration = new(TimeSpan.FromMilliseconds(200));

    private VirtualDesktopService? _service;
    private Themes.ThemeWatcher? _watcher;
    private readonly Dictionary<int, string> _names = new();
    private bool _showAllNames;
    private int _currentDesktop = 1;

    public DesktopsStrip()
    {
        InitializeComponent();
    }

    public int TileCount => Tiles.Items.Count;
    public bool ShowAllNames => _showAllNames;

    public void Attach(VirtualDesktopService service, Themes.ThemeWatcher watcher, bool showAllNames = false)
    {
        _service = service;
        _watcher = watcher;
        _showAllNames = showAllNames;
        var desktops = service.GetDesktops();
        _currentDesktop = service.GetCurrentDesktop().Number;
        Rebuild(desktops, _currentDesktop);
    }

    public void Rebuild(IReadOnlyList<DesktopInfo> desktops, int current)
    {
        _currentDesktop = current;
        Tiles.Items.Clear();
        _names.Clear();
        var style = (Style)Resources["TileButtonStyle"];
        foreach (var d in desktops)
        {
            var name = string.IsNullOrWhiteSpace(d.Name) ? d.Number.ToString() : d.Name;
            _names[d.Number] = name;
            var active = d.Number == current;
            var label = GetTileLabel(d.Number, active);
            var width = GetTileWidth(d.Number, active);
            var btn = new TileButton
            {
                Style = style,
                Content = MakeLabel(label),
                IsActive = active,
                Width = width,
                Tag = d.Number,
                ToolTip = string.IsNullOrWhiteSpace(d.Name) ? $"Desktop {d.Number}" : d.Name,
            };
            var number = d.Number;
            btn.Click += (_, _) =>
            {
                try
                {
                    _service?.SwitchDesktop(number);
                }
                catch
                {
                    // Shell hiccup; notification/poll will correct the UI.
                }
            };
            btn.ContextMenu = BuildTileMenu(btn, number, desktops.Count);
            Tiles.Items.Add(btn);
        }

        // No trailing gap after the last tile: content width is N*36-8, so the
        // native-sized window (N*36 wide) never clips the final tile.
        if (Tiles.Items.Count > 0 && Tiles.Items[^1] is TileButton last)
        {
            last.Margin = new Thickness(0);
        }
    }

    /// <summary>
    /// Tile context menu (adapted from YASB WorkspaceButton context menu):
    /// - Rename (opens YASB-styled floating popup input dialog)
    /// - Delete (disabled while a single desktop remains, like the shell itself)
    /// </summary>
    private ContextMenu BuildTileMenu(TileButton btn, int number, int desktopCount)
    {
        var rename = new MenuItem
        {
            Header = "Rename",
            // Segoe MDL2 / Fluent Rename glyph (\uE8AC), matching EverythingToolbar menu icons.
            Icon = "\uE8AC",
        };
        rename.Click += (_, _) =>
        {
            if (_service is null || _watcher is null)
            {
                return;
            }

            var currentName = _names.TryGetValue(number, out var name) ? name : $"Desktop {number}";
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                var dialog = new RenameWindow(_service, _watcher, number, currentName);
                dialog.ShowRelativeTo(btn);
            });
        };

        var delete = new MenuItem
        {
            Header = "Delete",
            // Segoe MDL2 delete glyph, like EverythingToolbar menu icons.
            Icon = "",
            IsEnabled = desktopCount > 1,
        };
        delete.Click += (_, _) =>
        {
            try
            {
                DeskLogger.Info($"delete desktop #{number}");
                _service?.DeleteDesktop(number);
            }
            catch (Exception ex)
            {
                DeskLogger.Info($"delete failed: {ex.Message}");
            }
        };
        return new ContextMenu { Items = { rename, delete } };
    }

    public void UpdateActive(int current)
    {
        _currentDesktop = current;
        foreach (var item in Tiles.Items)
        {
            if (item is not TileButton { Tag: int number } btn)
            {
                continue;
            }

            var isActive = number == current;
            if (btn.IsActive == isActive)
            {
                continue;
            }

            btn.IsActive = isActive;
            var label = GetTileLabel(number, isActive);
            btn.Content = MakeLabel(label);
            var targetWidth = GetTileWidth(number, isActive);
            AnimateWidth(btn, targetWidth);
        }
    }

    public void SetShowAllNames(bool showAllNames)
    {
        if (_showAllNames == showAllNames)
        {
            return;
        }

        _showAllNames = showAllNames;
        foreach (var item in Tiles.Items)
        {
            if (item is not TileButton { Tag: int number } btn)
            {
                continue;
            }

            var isActive = number == _currentDesktop;
            var label = GetTileLabel(number, isActive);
            btn.Content = MakeLabel(label);
            var targetWidth = GetTileWidth(number, isActive);
            AnimateWidth(btn, targetWidth);
        }
    }

    /// <summary>
    /// Re-measure pills once connected (correct DPI/font tokens).
    /// Called on window Loaded after the initial blind build.
    /// </summary>
    public void RefreshTileWidths()
    {
        foreach (var item in Tiles.Items)
        {
            if (item is not TileButton { Tag: int number } btn)
            {
                continue;
            }

            var isActive = number == _currentDesktop;
            var label = GetTileLabel(number, isActive);
            btn.Content = MakeLabel(label);
            btn.BeginAnimation(WidthProperty, null);
            btn.Width = GetTileWidth(number, isActive);
        }
    }

    /// <summary>
    /// Computes the exact target DIP width needed to display all tiles without clipping.
    /// </summary>
    public double ComputeDesiredContentWidth()
    {
        double width = 8; // Margin="8,0,0,0" on ItemsControl
        for (int i = 0; i < Tiles.Items.Count; i++)
        {
            if (Tiles.Items[i] is TileButton { Tag: int number })
            {
                var isActive = number == _currentDesktop;
                width += GetTileWidth(number, isActive);
                if (i < Tiles.Items.Count - 1)
                {
                    width += 8; // Margin="0,0,8,0" between tiles
                }
            }
        }
        return width;
    }

    private string GetTileLabel(int number, bool isActive)
    {
        var name = _names.TryGetValue(number, out var n) ? n : null;
        return DesktopLabelPolicy.GetLabel(number, name, isActive, _showAllNames);
    }

    private double GetTileWidth(int number, bool isActive)
    {
        if (_showAllNames || isActive)
        {
            var name = _names.TryGetValue(number, out var n) ? n : number.ToString();
            return CalculateTileWidth(name);
        }

        return InactiveWidth;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        MaxWidth = MaxActiveWidth - ActiveHorizontalPadding,
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
    };

    private double CalculateTileWidth(string label)
    {
        // Theme dictionaries may not be merged yet on first build; fall back
        // to matching literals rather than crashing (see Initialize order).
        var family =
            Resources["TileFontFamily"] as System.Windows.Media.FontFamily
            ?? new System.Windows.Media.FontFamily("Segoe UI Variable Text");
        var size = Resources["TileFontSize"] is double s ? s : 12.0;
        var weight = Resources["TileFontWeight"] is FontWeight w ? w : FontWeights.SemiBold;
        double pixelsPerDip = 1.0;
        try
        {
            pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch
        {
            // Not connected to a presentation source yet; fixed up on Loaded.
        }

        var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
        var text = new FormattedText(
            label,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            size,
            System.Windows.Media.Brushes.Black,
            pixelsPerDip);
        return Math.Clamp(text.Width + ActiveHorizontalPadding, InactiveWidth, MaxActiveWidth);
    }

    private static void AnimateWidth(TileButton btn, double to)
    {
        var from = double.IsNaN(btn.Width) ? (btn.ActualWidth > 0 ? btn.ActualWidth : InactiveWidth) : btn.Width;
        if (Math.Abs(from - to) < 0.5)
        {
            btn.BeginAnimation(WidthProperty, null);
            btn.Width = to;
            return;
        }

        btn.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(from, to, GrowDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
    }
}
