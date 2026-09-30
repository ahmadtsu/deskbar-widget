// Adapted from EverythingToolbar theme concepts (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
// NOTE (build): no App.csproj change is needed for Light.xaml / Dark.xaml —
// the SDK-style WPF project globs *.xaml as Page (Resource) automatically.
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DeskbarWidget.App.Themes;

internal static class TileTheme
{
    private static readonly ConditionalWeakTable<FrameworkElement, List<ResourceDictionary>> s_added = new();

    public static void ApplyTo(FrameworkElement root, ThemeSnapshot theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(theme);

        if (s_added.TryGetValue(root, out List<ResourceDictionary>? added))
        {
            foreach (ResourceDictionary dict in added)
                root.Resources.MergedDictionaries.Remove(dict);
            added.Clear();
        }
        else
        {
            added = new List<ResourceDictionary>();
            s_added.Add(root, added);
        }

        string name = theme.IsLight ? "Light" : "Dark";
        var themeDict = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/DeskbarWidget.App;component/Themes/{name}.xaml",
                UriKind.Absolute),
        };
        root.Resources.MergedDictionaries.Add(themeDict);
        added.Add(themeDict);

        System.Windows.Media.Color accent = theme.Accent;
        System.Windows.Media.Color foreground = IsLight(accent)
            ? System.Windows.Media.Colors.Black
            : System.Windows.Media.Colors.White;

        var activeBackground = new SolidColorBrush(accent);
        var activeForeground = new SolidColorBrush(foreground);
        activeBackground.Freeze();
        activeForeground.Freeze();

        var activeDict = new ResourceDictionary
        {
            ["TileActiveBackground"] = activeBackground,
            ["TileActiveForeground"] = activeForeground,
        };
        root.Resources.MergedDictionaries.Add(activeDict);
        added.Add(activeDict);
    }

    private static bool IsLight(System.Windows.Media.Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 150;
}
