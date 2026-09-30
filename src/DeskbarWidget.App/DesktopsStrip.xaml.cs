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

    public DesktopsStrip()
    {
        InitializeComponent();
    }

    public int TileCount => Tiles.Items.Count;

    public void Attach(VirtualDesktopService service, Themes.ThemeWatcher watcher)
    {
        _service = service;
        _watcher = watcher;
        var desktops = service.GetDesktops();
        Rebuild(desktops, service.GetCurrentDesktop().Number);
    }

    public void Rebuild(IReadOnlyList<DesktopInfo> desktops, int current)
    {
        Tiles.Items.Clear();
        _names.Clear();
        var style = (Style)Resources["TileButtonStyle"];
        foreach (var d in desktops)
        {
            var name = string.IsNullOrWhiteSpace(d.Name) ? d.Number.ToString() : d.Name;
            _names[d.Number] = name;
            var active = d.Number == current;
            var btn = new TileButton
            {
                Style = style,
                Content = MakeLabel(active ? name : d.Number.ToString()),
                IsActive = active,
                Width = active ? ActiveWidth(name) : InactiveWidth,
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
        if (Tiles.Items[^1] is TileButton last)
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

    public void UpdateActive(int current)    {
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

            var label = isActive && _names.TryGetValue(number, out var name) ? name : number.ToString();
            btn.Content = MakeLabel(label);
            btn.IsActive = isActive;
            AnimateWidth(btn, isActive ? ActiveWidth(label) : InactiveWidth);
        }
    }

    /// <summary>
    /// Re-measure active pills once connected (correct DPI/font tokens).
    /// Called on window Loaded after the initial blind build.
    /// </summary>
    public void RefreshActiveWidths()
    {
        foreach (var item in Tiles.Items)
        {
            if (item is not TileButton { Tag: int number, IsActive: true } btn)
            {
                continue;
            }

            if (_names.TryGetValue(number, out var name))
            {
                btn.Content = MakeLabel(name);
                btn.BeginAnimation(WidthProperty, null);
                btn.Width = ActiveWidth(name);
            }
        }
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        MaxWidth = MaxActiveWidth - ActiveHorizontalPadding,
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
    };

    private double ActiveWidth(string label)
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
        btn.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(btn.ActualWidth, to, GrowDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
    }
}
