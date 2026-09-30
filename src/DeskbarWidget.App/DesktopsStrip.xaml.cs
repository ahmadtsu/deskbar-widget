using System.Windows;
using System.Windows.Controls;
using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// Horizontal row of squircle number tiles. Code-behind driven (no MVVM
/// package): Rebuild on DesktopsUpdated, UpdateActive on DesktopChanged.
/// Active tile is flagged via Button.Tag=true (see TileButtonStyle trigger).
/// </summary>
internal partial class DesktopsStrip
{
    private VirtualDesktopService? _service;

    public DesktopsStrip()
    {
        InitializeComponent();
    }

    public int TileCount => Tiles.Items.Count;

    public void Attach(VirtualDesktopService service)
    {
        _service = service;
        Rebuild(service.GetDesktops(), service.GetCurrentDesktop().Number);
    }

    public void Rebuild(IReadOnlyList<DesktopInfo> desktops, int current)
    {
        Tiles.Items.Clear();
        var style = (Style)Resources["TileButtonStyle"];
        foreach (var d in desktops)
        {
            var btn = new Button
            {
                Style = style,
                Content = d.Number.ToString(),
                Tag = d.Number == current,
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
            Tiles.Items.Add(btn);
        }

        // No trailing gap after the last tile: content width is N*36-8, so the
        // native-sized window (N*36 wide) never clips the final tile.
        if (Tiles.Items[^1] is Button last)
        {
            last.Margin = new Thickness(0);
        }
    }

    public void UpdateActive(int current)
    {
        foreach (var item in Tiles.Items)
        {
            if (item is Button btn && int.TryParse(btn.Content?.ToString(), out var n))
            {
                btn.Tag = n == current;
            }
        }
    }
}
