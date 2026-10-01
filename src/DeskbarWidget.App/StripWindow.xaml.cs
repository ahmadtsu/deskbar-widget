// Adapted from EverythingToolbar TaskbarWindow/TaskbarWindowHost concepts
// (MIT © 2023 Stephan Rumswinkel). See ATTRIBUTION.md.
// Borderless window docked into the taskbar as WS_CHILD; measures the free gap
// via TaskbarLayoutProbe and glides with TaskbarWindowAnimator. Falls back to a
// standalone top-edge bar when the taskbar refuses hosting.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskbarWidget.App.Taskbar;
using DeskbarWidget.App.Themes;
using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.App;

internal partial class StripWindow : Window
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    private const int AttachRetryCount = 5;
    private const int RepositionMs = 150;
    private const int SettleMs = 1200;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    private delegate void WinEventProc(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    private readonly VirtualDesktopService _service;
    private readonly ThemeWatcher _watcher;
    private readonly string _alignment;
    private bool _showAllNames;
    private readonly TaskbarLayoutProbe _probe = new();
    private TaskbarWindowAnimator? _animator;

    private readonly EventHandler<int> _desktopChangedHandler;
    private readonly EventHandler<DesktopsUpdatedEventArgs> _desktopsUpdatedHandler;
    private readonly EventHandler<ThemeSnapshot> _themeChangedHandler;

    private nint _handle;
    private nint _taskbarHandle;
    private readonly bool _isSecondary;
    private bool _docked;
    private bool _standalone;
    private int _attachRetriesLeft = AttachRetryCount;
    private DispatcherTimer? _dockRetryTimer;
    private int _positionGeneration;
    private bool _refreshLayoutElements = true;
    private DateTime _settleDeadline;
    private uint _taskbarCreatedMsg;

    private Rect _lastTaskbarRect;
    private TaskbarLayout? _lastLayout;
    private double _lastDpi = 1.0;
    private bool _lastCenterAligned;
    private DispatcherTimer? _shrinkTimer;

    private readonly DispatcherTimer _repositionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(RepositionMs),
    };
    private WinEventProc? _winEventCallback;
    private nint _winEventHook;

    public nint TaskbarHandle => _taskbarHandle;
    public bool IsSecondary => _isSecondary;

    public StripWindow(
        VirtualDesktopService service,
        ThemeWatcher watcher,
        string alignment,
        bool showAllNames = false,
        nint taskbarHandle = 0)
    {
        _service = service;
        _watcher = watcher;
        _alignment = alignment == "Left" ? "Left" : "Right";
        _showAllNames = showAllNames;
        _taskbarHandle = taskbarHandle;
        _isSecondary = taskbarHandle != nint.Zero && NativeTaskbar.IsSecondaryTaskbar(taskbarHandle);

        InitializeComponent();

        _repositionTimer.Tick += (_, _) =>
        {
            _repositionTimer.Stop();
            var refresh = _refreshLayoutElements;
            _refreshLayoutElements = true;
            UpdatePosition(refresh);
        };

        Loaded += (_, _) =>
        {
            Strip.RefreshTileWidths();
            UpdatePosition();
        };
        Closed += (_, _) => Cleanup();
        MouseDown += (_, e) =>
        {
            if (_standalone && e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        };

        _desktopChangedHandler = (_, index) => Dispatcher.BeginInvoke(() => HandleDesktopChanged(index));
        _desktopsUpdatedHandler = (_, e) => Dispatcher.BeginInvoke(() =>
        {
            Strip.Rebuild(_service.GetDesktops(), e.Index);
            if (_standalone)
            {
                Width = Math.Max(240, Strip.ComputeDesiredContentWidth() + 16);
            }
            UpdatePosition();
        });
        _themeChangedHandler = (_, snapshot) => Dispatcher.BeginInvoke(() =>
        {
            var a = snapshot.Accent;
            DeskLogger.Info($"theme: light={snapshot.IsLight} accent=#{a.R:X2}{a.G:X2}{a.B:X2}");
            TileTheme.ApplyTo(Strip, snapshot);
        });

        _service.DesktopChanged += _desktopChangedHandler;
        _service.DesktopsUpdated += _desktopsUpdatedHandler;
        _watcher.Changed += _themeChangedHandler;
    }

    public void SetShowAllNames(bool showAllNames)
    {
        _showAllNames = showAllNames;
        Dispatcher.BeginInvoke(() =>
        {
            var currentDip = ActualWidth > 0 ? ActualWidth : Strip.ComputeDesiredContentWidth();
            var targetDip = Strip.ComputeDesiredContentWidth(targetShowAllNames: showAllNames);

            _shrinkTimer?.Stop();

            if (targetDip >= currentDip - 0.5)
            {
                ResizeToContent(targetDip);
            }

            Strip.SetShowAllNames(showAllNames);

            if (targetDip < currentDip - 0.5)
            {
                _shrinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
                _shrinkTimer.Tick += (_, _) =>
                {
                    _shrinkTimer.Stop();
                    ResizeToContent(Strip.ComputeDesiredContentWidth());
                };
                _shrinkTimer.Start();
            }
        });
    }

    private void HandleDesktopChanged(int newIndex)
    {
        var currentDip = ActualWidth > 0 ? ActualWidth : Strip.ComputeDesiredContentWidth();
        var targetDip = Strip.ComputeDesiredContentWidth(targetActiveDesktop: newIndex);

        _shrinkTimer?.Stop();

        if (targetDip >= currentDip - 0.5)
        {
            // Expanding (or equal): immediately widen the window so the growing
            // pill is never clipped by the window boundary during its animation.
            ResizeToContent(targetDip);
        }

        Strip.UpdateActive(newIndex);

        if (targetDip < currentDip - 0.5)
        {
            // Shrinking: keep the window wide while the old active pill
            // smoothly shrinks over 200ms, then trim the transparent excess.
            _shrinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _shrinkTimer.Tick += (_, _) =>
            {
                _shrinkTimer.Stop();
                ResizeToContent(Strip.ComputeDesiredContentWidth());
            };
            _shrinkTimer.Start();
        }
    }

    private void ResizeToContent(double desiredContentDip)
    {
        if (_standalone)
        {
            Width = Math.Max(240, desiredContentDip + 16);
            return;
        }

        if (!_docked || _handle == nint.Zero || _taskbarHandle == nint.Zero)
        {
            return;
        }

        if (_lastLayout is not { IsMeasurable: true } layout)
        {
            UpdatePosition();
            return;
        }

        var contentPx = desiredContentDip * _lastDpi;
        var tiles = Strip.TileCount;
        var bounds = StripBounds.Calculate(
            _taskbarHandle,
            _lastTaskbarRect,
            layout,
            _lastDpi,
            _alignment,
            _lastCenterAligned,
            tiles,
            contentPx + _lastDpi);

        if (bounds is { } b)
        {
            Width = b.Width / _lastDpi;
            Height = b.Height / _lastDpi;
            _animator?.SetBounds(b);
        }
    }

    public void Initialize()
    {
        // Force HWND creation now: TryDock needs a real handle for
        // GetWindowLong/SetParent (it is otherwise only created on Show()).
        // This also fires OnSourceInitialized (animator + taskbar hook setup).
        _ = new WindowInteropHelper(this).EnsureHandle();

        // Theme first: tile measuring reads theme font tokens.
        var theme = _watcher.Current;
        TileTheme.ApplyTo(Strip, theme);
        Strip.Attach(_service, _watcher, _showAllNames, _alignment);
        DeskLogger.Info($"handle=0x{_handle:X}, tiles={Strip.TileCount}, light={theme.IsLight} accent=#{theme.Accent.R:X2}{theme.Accent.G:X2}{theme.Accent.B:X2} showAllNames={_showAllNames}");
        TryDock();
    }

    // ---- docking ----

    private void TryDock()
    {
        if (!CanDock())
        {
            DeskLogger.Info(
                $"TryDock: not dockable (build={Environment.OSVersion.Version.Build} " +
                $"classic={NativeTaskbar.IsClassicTaskbar(_taskbarHandle)} " +
                $"tray=0x{_taskbarHandle:X} secondary={_isSecondary})");
            ScheduleRetryOrFallback();
            return;
        }

        if (_taskbarHandle == nint.Zero)
        {
            _taskbarHandle = NativeTaskbar.FindTaskbarHandle();
        }

        if (_taskbarHandle == nint.Zero || !NativeTaskbar.IsWindow(_taskbarHandle))
        {
            DeskLogger.Info($"TryDock: Taskbar 0x{_taskbarHandle:X} not found or invalid, scheduling retry");
            ScheduleRetryOrFallback();
            return;
        }

        var style = GetWindowLong(_handle, GWL_STYLE);
        SetWindowLong(_handle, GWL_STYLE, (style & ~WS_POPUP) | WS_CHILD);
        var exStyle = GetWindowLong(_handle, GWL_EXSTYLE);
        SetWindowLong(_handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        if (NativeTaskbar.SetParent(_handle, _taskbarHandle) == nint.Zero)
        {
            DeskLogger.Info("TryDock: SetParent failed");
            ScheduleRetryOrFallback();
            return;
        }

        _docked = true;
        DeskLogger.Info($"TryDock: docked to taskbar 0x{_taskbarHandle:X} (secondary={_isSecondary})");
        _attachRetriesLeft = AttachRetryCount;
        HookTaskbarEvents();
        Show();
        UpdatePosition();
    }

    private bool CanDock() =>
        Environment.OSVersion.Version.Build >= 22000 && !NativeTaskbar.IsClassicTaskbar(_taskbarHandle);

    private void ScheduleRetryOrFallback()
    {
        DeskLogger.Info($"ScheduleRetryOrFallback: retries left={_attachRetriesLeft} isSecondary={_isSecondary}");
        if (_attachRetriesLeft-- > 0)
        {
            _dockRetryTimer?.Stop();
            _dockRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _dockRetryTimer.Tick += (_, _) =>
            {
                DeskLogger.Info("Dock retry tick");
                _dockRetryTimer.Stop();
                if (_handle == nint.Zero)
                {
                    return;
                }

                TryDock();
            };
            _dockRetryTimer.Start();
            return;
        }

        if (_isSecondary)
        {
            DeskLogger.Info($"Secondary taskbar 0x{_taskbarHandle:X} docking failed; closing window");
            Close();
            return;
        }

        FallbackStandalone();
    }

    private void FallbackStandalone()
    {
        _standalone = true;
        Width = Math.Max(240, Strip.ComputeDesiredContentWidth() + 16);
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Top + 8;
        DeskLogger.Info($"FallbackStandalone at {Left},{Top}");
        Show();
    }

    public void HandleDisplaySettingsChanged()
    {
        if (_standalone || _handle == nint.Zero)
        {
            return;
        }

        DeskLogger.Info($"StripWindow (0x{_handle:X}, taskbar=0x{_taskbarHandle:X}): HandleDisplaySettingsChanged");
        _probe.Invalidate();
        _lastLayout = null;
        if (_taskbarHandle != nint.Zero && NativeTaskbar.IsWindow(_taskbarHandle))
        {
            _lastDpi = NativeTaskbar.GetDpiForWindow(_taskbarHandle) / 96.0;
        }

        _settleDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(SettleMs);
        _refreshLayoutElements = true;
        _repositionTimer.Stop();
        _repositionTimer.Start();
        UpdatePosition(refreshLayoutElements: true);
    }

    // ---- positioning ----

    private void UpdatePosition(bool refreshLayoutElements = true)
    {
        if (_standalone || _handle == nint.Zero)
        {
            return;
        }

        if (!_docked)
        {
            return;
        }

        if (_taskbarHandle == nint.Zero)
        {
            _taskbarHandle = NativeTaskbar.FindTaskbarHandle();
        }

        if (_taskbarHandle == nint.Zero || !NativeTaskbar.IsWindow(_taskbarHandle))
        {
            if (_isSecondary)
            {
                DeskLogger.Info($"Secondary taskbar 0x{_taskbarHandle:X} no longer valid; closing window");
                Close();
            }
            return;
        }

        if (NativeTaskbar.GetParent(_handle) != _taskbarHandle)
        {
            NativeTaskbar.SetParent(_handle, _taskbarHandle);
        }

        if (!NativeTaskbar.TryGetVisibleTaskbarRect(_taskbarHandle, out var taskbarRect))
        {
            if (DateTime.UtcNow < _settleDeadline)
            {
                _repositionTimer.Start();
            }
            return;
        }

        var generation = ++_positionGeneration;
        var handle = _taskbarHandle;
        var tiles = Strip.TileCount;
        var alignment = _alignment;
        var centerAligned = IsTaskbarCenterAligned();
        var dpi = NativeTaskbar.GetDpiForWindow(handle) / 96.0;

        Task.Run(() =>
        {
            var layout = _probe.Measure(handle, refreshLayoutElements);
            Dispatcher.BeginInvoke(() => ApplyLayout(handle, taskbarRect, layout, dpi, alignment, centerAligned, tiles, generation));
        });
    }

    private void ApplyLayout(
        nint handle, Rect taskbarRect, TaskbarLayout layout,
        double dpi, string alignment, bool centerAligned, int tiles, int generation)
    {
        if (generation != _positionGeneration || _animator is null)
        {
            return;
        }

        if (DateTime.UtcNow < _settleDeadline)
        {
            _refreshLayoutElements = false;
            _repositionTimer.Stop();
            _repositionTimer.Start();
        }

        if (!layout.IsMeasurable)
        {
            DeskLogger.Info("probe: nothing measurable yet");
            return;
        }

        _lastTaskbarRect = taskbarRect;
        _lastLayout = layout;
        _lastDpi = dpi;
        _lastCenterAligned = centerAligned;

        DeskLogger.Info(
            $"probe: align={alignment} centered={centerAligned} dpi={dpi:F2} " +
            $"taskbar={taskbarRect} cluster={Fmt(layout.IconCluster)} " +
            $"obstacles={layout.Obstacles.Count} tiles={tiles}");

        // Measure the rendered content so the HWND is sized to what WPF draws
        // (plus 1px slack), never the other way around.
        Strip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var contentDip = Math.Max(Strip.DesiredSize.Width, Strip.ComputeDesiredContentWidth());
        var contentPx = contentDip * dpi;

        var bounds = StripBounds.Calculate(handle, taskbarRect, layout, dpi, alignment, centerAligned, tiles, contentPx + dpi);
        if (bounds is { } b)
        {
            NativeTaskbar.GetWindowRect(_handle, out var hr);
            DeskLogger.Info(
                $"placed at {b.Left},{b.Top} {b.Width}x{b.Height} " +
                $"(tiles={tiles} content={contentPx:F1}px win={ActualWidth:F1}x{ActualHeight:F1} " +
                $"hwnd={hr.Right - hr.Left}x{hr.Bottom - hr.Top})");
            // Keep the WPF layout size in sync with the native HWND size
            // (physical px -> DIPs) so tiles are never clipped by the edge.
            Width = b.Width / dpi;
            Height = b.Height / dpi;
            _animator.MoveTo(b);
        }
        else
        {
            DeskLogger.Info($"no room for {tiles} tiles; hiding");
            _animator.Hide();
        }
    }

    private static string Fmt(Rect? r) =>
        r is { } v ? $"{v.Left:F0},{v.Top:F0} {v.Width:F0}x{v.Height:F0}" : "none";

    private static bool IsTaskbarCenterAligned()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            return (int?)key?.GetValue("TaskbarAl", 1) != 0;
        }
        catch
        {
            return true;
        }
    }

    // ---- taskbar event tracking ----

    private void HookTaskbarEvents()
    {
        if (_taskbarHandle == nint.Zero || _winEventCallback is not null)
        {
            return;
        }

        var tid = NativeTaskbar.GetWindowThreadProcessId(_taskbarHandle, out var pid);
        if (tid == 0)
        {
            return;
        }

        _winEventCallback = (_, _, _, _, _, _, _) => Dispatcher.BeginInvoke(() =>
        {
            _settleDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(SettleMs);
            _repositionTimer.Stop();
            _repositionTimer.Start();
        });
        _winEventHook = SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            nint.Zero, _winEventCallback, pid, tid, WINEVENT_OUTOFCONTEXT);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _handle = new WindowInteropHelper(this).Handle;
        _animator = new TaskbarWindowAnimator(_handle, () => !SystemParameters.ClientAreaAnimation);

        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_DPICHANGED = 0x02E0;

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg is WM_DISPLAYCHANGE or WM_DPICHANGED)
        {
            HandleDisplaySettingsChanged();
            return nint.Zero;
        }

        if (_taskbarCreatedMsg != 0 && msg == _taskbarCreatedMsg)
        {
            if (_isSecondary)
            {
                // Secondary taskbars will be recreated and re-synchronized by StripWindowManager
                Close();
                return nint.Zero;
            }

            // Explorer restarted: give the new taskbar a moment, then re-dock.
            _docked = false;
            _taskbarHandle = nint.Zero;
            _attachRetriesLeft = AttachRetryCount;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                TryDock();
            };
            t.Start();
        }

        return nint.Zero;
    }

    private void Cleanup()
    {
        _service.DesktopChanged -= _desktopChangedHandler;
        _service.DesktopsUpdated -= _desktopsUpdatedHandler;
        _watcher.Changed -= _themeChangedHandler;
        _repositionTimer.Stop();
        _shrinkTimer?.Stop();
        _dockRetryTimer?.Stop();
        if (_winEventHook != nint.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = nint.Zero;
        }

        _winEventCallback = null;
    }
}
