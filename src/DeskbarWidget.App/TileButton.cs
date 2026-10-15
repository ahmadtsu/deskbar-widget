using System.Windows;
using System.Windows.Controls;

namespace DeskbarWidget.App;

/// <summary>
/// Tile with a typed active flag. (Button.Tag was tried first, but a Trigger
/// on Tag never fires: Tag is object and XAML Value="True" stays a string,
/// so the comparison against bool true always fails.)
/// </summary>
internal sealed class TileButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive), typeof(bool), typeof(TileButton),
            new PropertyMetadata(false));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(
            nameof(IsLocked), typeof(bool), typeof(TileButton),
            new PropertyMetadata(false));

    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }
}
