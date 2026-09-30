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
    private readonly TaskbarLayoutProbe _probe = new();
    private TaskbarWindowAnimator? _animator;

    private nint _handle;
    private nint _taskbarHandle;
    private bool _docked;
    private bool _standalone;
    private int _attachRetriesLeft = AttachRetryCount;
    private DispatcherTimer? _dockRetryTimer;
    private int _positionGeneration;
    private bool _refreshLayoutElements = true;
    private DateTime _settleDeadline;
    private uint _taskbarCreatedMsg;

    private readonly DispatcherTimer _repositionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(RepositionMs),
    };
    private WinEventProc? _winEventCallback;
    private nint _winEventHook;

    public StripWindow(VirtualDesktopService service, ThemeWatcher watcher, string alignment)
    {
        _service = service;
        _watcher = watcher;
        _alignment = alignment == "Left" ? "Left" : "Right";

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
            Strip.RefreshActiveWidths();
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

        _service.DesktopChanged += (_, index) => Dispatcher.BeginInvoke(() =>
        {
            Strip.UpdateActive(index);
            // The active pill grows over ~200ms; re-fit the window once done.
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                UpdatePosition();
            };
            t.Start();
        });
        _service.DesktopsUpdated += (_, e) => Dispatcher.BeginInvoke(() =>
        {
            Strip.Rebuild(_service.GetDesktops(), e.Index);
            UpdatePosition();
        });
        _watcher.Changed += (_, snapshot) => Dispatcher.BeginInvoke(() =>
        {
            var a = snapshot.Accent;
            DeskLogger.Info($"theme: light={snapshot.IsLight} accent=#{a.R:X2}{a.G:X2}{a.B:X2}");
            TileTheme.ApplyTo(Strip, snapshot);
        });
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
        Strip.Attach(_service, _watcher);
        DeskLogger.Info($"handle=0x{_handle:X}, tiles={Strip.TileCount}, light={theme.IsLight} accent=#{theme.Accent.R:X2}{theme.Accent.G:X2}{theme.Accent.B:X2}");
        TryDock();
    }

    // ---- docking ----

    private void TryDock()
    {
        if (!CanDock())
        {
            DeskLogger.Info(
                $"TryDock: not dockable (build={Environment.OSVersion.Version.Build} " +
                $"classic={NativeTaskbar.IsClassicTaskbar()} " +
                $"tray=0x{NativeTaskbar.FindTaskbarHandle():X})");
            ScheduleRetryOrFallback();
            return;
        }

        _taskbarHandle = NativeTaskbar.FindTaskbarHandle();
        if (_taskbarHandle == nint.Zero)
        {
            DeskLogger.Info("TryDock: Shell_TrayWnd not found, using standalone mode");
            FallbackStandalone();
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
        DeskLogger.Info($"TryDock: docked to taskbar 0x{_taskbarHandle:X}");
        _attachRetriesLeft = AttachRetryCount;
        HookTaskbarEvents();
        Show();
        UpdatePosition();
    }

    private static bool CanDock() =>
        Environment.OSVersion.Version.Build >= 22000 && !NativeTaskbar.IsClassicTaskbar();

    private void ScheduleRetryOrFallback()
    {
        DeskLogger.Info($"ScheduleRetryOrFallback: retries left={_attachRetriesLeft}");
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

        FallbackStandalone();
    }

    private void FallbackStandalone()
    {
        _standalone = true;
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Top + 8;
        DeskLogger.Info($"FallbackStandalone at {Left},{Top}");
        Show();
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

        if (_taskbarHandle == nint.Zero)
        {
            return;
        }

        if (NativeTaskbar.GetParent(_handle) != _taskbarHandle)
        {
            NativeTaskbar.SetParent(_handle, _taskbarHandle);
        }

        if (!NativeTaskbar.GetWindowRect(_taskbarHandle, out var r))
        {
            return;
        }

        var taskbarRect = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        if (taskbarRect.Width <= 0 || taskbarRect.Height <= 0)
        {
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

        DeskLogger.Info(
            $"probe: align={alignment} centered={centerAligned} dpi={dpi:F2} " +
            $"taskbar={taskbarRect} cluster={Fmt(layout.IconCluster)} " +
            $"obstacles={layout.Obstacles.Count} tiles={tiles}");

        // Measure the rendered content so the HWND is sized to what WPF draws
        // (plus 1px slack), never the other way around.
        Strip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var contentPx = Strip.DesiredSize.Width * dpi;

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

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_taskbarCreatedMsg != 0 && msg == _taskbarCreatedMsg)
        {
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
        _repositionTimer.Stop();
        if (_winEventHook != nint.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = nint.Zero;
        }

        _winEventCallback = null;
    }
}
