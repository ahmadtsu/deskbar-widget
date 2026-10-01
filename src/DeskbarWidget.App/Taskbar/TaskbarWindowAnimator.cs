// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
using System.Windows;
using System.Windows.Media.Animation;

namespace DeskbarWidget.App.Taskbar;

/// <summary>
/// Moves and shows/hides a window that is placed with SetWindowPos rather than through WPF layout.
/// Size and position changes glide instead of jumping.
///
/// Must be constructed and driven on the UI thread (owns a WPF Animatable).
/// </summary>
internal sealed class TaskbarWindowAnimator
{
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(250);

    private readonly nint _hwnd;
    private readonly Func<bool> _animationsDisabled;
    private readonly Progress _progress = new();

    // Null while the window is off screen: before its first placement, and after Hide.
    private WidgetBounds? _current;
    private WidgetBounds? _target;
    private WidgetBounds _from;

    public TaskbarWindowAnimator(nint hwnd, Func<bool> animationsDisabled)
    {
        _hwnd = hwnd;
        _animationsDisabled = animationsDisabled;
        _progress.ValueChanged += OnProgressChanged;
    }

    public WidgetBounds? CurrentBounds => _current;

    public void SetBounds(WidgetBounds bounds, bool ensureVisible = false)
    {
        _from = bounds;
        _target = bounds;
        StopAnimation();
        ApplyBounds(bounds, ensureVisible);
    }

    public void MoveTo(WidgetBounds target)
    {
        if (_target == target)
        {
            return;
        }

        _target = target;

        // A window that is not on screen yet has nothing to glide from, so it appears in place.
        if (_current is not { } from || _animationsDisabled())
        {
            // Pinning the start at the target keeps the progress reset below from moving the window.
            _from = target;
            StopAnimation();
            ApplyBounds(target, ensureVisible: true);
            return;
        }

        // If only size changed but position didn't, don't animate the HWND bounds with SetWindowPos
        // (WPF handles content transitions smoothly; animating HWND size creates a moving clip rect).
        if (from.Left == target.Left && from.Top == target.Top)
        {
            _from = target;
            StopAnimation();
            ApplyBounds(target);
            return;
        }

        // Starting wherever the window is right now lets a target picked up mid-flight continue smoothly.
        _from = from;

        _progress.Animate(
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = AnimationDuration,
                EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut, Power = 5 },
            }
        );
    }

    public bool Hide()
    {
        var wasPositioned = _current is not null;

        _current = null;
        _target = null;
        StopAnimation();

        NativeTaskbar.SetWindowPos(
            _hwnd,
            0,
            0,
            0,
            0,
            NativeTaskbar.SWP_NOMOVE
                | NativeTaskbar.SWP_NOSIZE
                | NativeTaskbar.SWP_NOZORDER
                | NativeTaskbar.SWP_NOACTIVATE
                | NativeTaskbar.SWP_HIDEWINDOW
        );
        return wasPositioned;
    }

    // A finished animation holds its end value without ticking, so it is only cancelled where that
    // held value would get in the way.
    public void StopAnimation() => _progress.Animate(null);

    private void OnProgressChanged(double progress)
    {
        if (_target is { } target)
        {
            ApplyBounds(WidgetBounds.Lerp(_from, target, progress));
        }
    }

    private void ApplyBounds(WidgetBounds bounds, bool ensureVisible = false)
    {
        _current = bounds;

        uint flags = NativeTaskbar.SWP_NOZORDER | NativeTaskbar.SWP_NOACTIVATE;
        if (ensureVisible)
        {
            flags |= NativeTaskbar.SWP_SHOWWINDOW;
        }

        NativeTaskbar.SetWindowPos(_hwnd, bounds.Left, bounds.Top, bounds.Width, bounds.Height, flags);
    }

    /// <summary>
    /// A single animatable double. WPF animates dependency properties, and window bounds set through
    /// SetWindowPos are not one, so the transition drives this and the bounds follow from it.
    /// </summary>
    private sealed class Progress : Animatable
    {
        private static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value",
            typeof(double),
            typeof(Progress),
            new PropertyMetadata(0.0, (d, e) => ((Progress)d).ValueChanged?.Invoke((double)e.NewValue))
        );

        public event Action<double>? ValueChanged;

        public void Animate(AnimationTimeline? animation) => BeginAnimation(ValueProperty, animation);

        protected override Freezable CreateInstanceCore() => new Progress();
    }
}
