// Multi-monitor manager for DeskbarWidget StripWindow instances across displays.
// Spawns instances across Shell_TrayWnd and Shell_SecondaryTrayWnd taskbars.
using System.Diagnostics;
using System.Windows.Threading;
using DeskbarWidget.App.Taskbar;
using DeskbarWidget.App.Themes;
using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.App;

/// <summary>
/// Manages the lifecycle of <see cref="StripWindow"/> instances across multiple displays,
/// docking into <c>Shell_TrayWnd</c> on the primary monitor and any <c>Shell_SecondaryTrayWnd</c>
/// taskbars on secondary monitors.
/// </summary>
internal sealed class StripWindowManager : IDisposable
{
    private readonly VirtualDesktopService _service;
    private readonly ThemeWatcher _watcher;
    private readonly string _alignment;
    private bool _showAllNames;
    private readonly Dictionary<nint, StripWindow> _windows = new();
    private readonly DispatcherTimer _syncTimer;
    private StripWindow? _standaloneWindow;
    private bool _disposed;

    public StripWindowManager(
        VirtualDesktopService service,
        ThemeWatcher watcher,
        string alignment,
        bool showAllNames = false)
    {
        _service = service;
        _watcher = watcher;
        _alignment = alignment;
        _showAllNames = showAllNames;

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _syncTimer.Tick += (_, _) => SyncTaskbars();

        try
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch (Exception ex)
        {
            DeskLogger.Info($"StripWindowManager: SystemEvents hook failed: {ex.Message}");
        }
    }

    public int WindowCount => _windows.Count + (_standaloneWindow != null ? 1 : 0);

    public IReadOnlyCollection<StripWindow> Windows => _windows.Values;

    public void Initialize()
    {
        SyncTaskbars();
        _syncTimer.Start();
    }

    public void SetShowAllNames(bool showAllNames)
    {
        _showAllNames = showAllNames;
        foreach (var window in _windows.Values.ToList())
        {
            window.SetShowAllNames(showAllNames);
        }

        _standaloneWindow?.SetShowAllNames(showAllNames);
    }

    public void SyncTaskbars()
    {
        if (_disposed)
        {
            return;
        }

        var currentTaskbars = NativeTaskbar.FindAllTaskbarHandles();
        DeskLogger.Info($"SyncTaskbars: found {currentTaskbars.Count} active taskbar(s)");

        // 1. Remove dead, invalid, or disappeared taskbar windows
        var deadHandles = new List<nint>();
        foreach (var (handle, window) in _windows)
        {
            if (!currentTaskbars.Contains(handle) || !NativeTaskbar.IsWindow(handle))
            {
                deadHandles.Add(handle);
            }
        }

        foreach (var dead in deadHandles)
        {
            DeskLogger.Info($"SyncTaskbars: removing dead taskbar 0x{dead:X}");
            if (_windows.Remove(dead, out var window))
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    DeskLogger.Info($"Error closing dead window: {ex.Message}");
                }
            }
        }

        // 2. Add newly discovered taskbars
        foreach (var handle in currentTaskbars)
        {
            if (!_windows.ContainsKey(handle))
            {
                DeskLogger.Info($"SyncTaskbars: spawning StripWindow for taskbar 0x{handle:X}");
                var window = new StripWindow(_service, _watcher, _alignment, _showAllNames, handle);
                var localHandle = handle;
                window.Closed += (_, _) =>
                {
                    _windows.Remove(localHandle);
                };
                _windows[handle] = window;
                window.Initialize();
            }
        }

        // 3. Fallback: if no taskbar could be found and none are open, create primary / fallback window
        if (_windows.Count == 0 && _standaloneWindow == null)
        {
            var primary = NativeTaskbar.FindTaskbarHandle();
            DeskLogger.Info($"SyncTaskbars: no taskbars active, creating fallback window (primary=0x{primary:X})");
            var window = new StripWindow(_service, _watcher, _alignment, _showAllNames, primary);
            if (primary != nint.Zero && NativeTaskbar.IsWindow(primary))
            {
                _windows[primary] = window;
                window.Closed += (_, _) => _windows.Remove(primary);
            }
            else
            {
                _standaloneWindow = window;
                window.Closed += (_, _) =>
                {
                    if (_standaloneWindow == window)
                    {
                        _standaloneWindow = null;
                    }
                };
            }

            window.Initialize();
        }
        else if (_standaloneWindow != null && _windows.Count > 0)
        {
            // If taskbars have appeared while running standalone, close the standalone fallback
            try
            {
                _standaloneWindow.Close();
            }
            catch { }
            _standaloneWindow = null;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DeskLogger.Info("StripWindowManager: DisplaySettingsChanged detected");
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            delay.Tick += (_, _) =>
            {
                delay.Stop();
                SyncTaskbars();
            };
            delay.Start();
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _syncTimer.Stop();

        try
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
        catch { }

        foreach (var window in _windows.Values.ToList())
        {
            try
            {
                window.Close();
            }
            catch { }
        }
        _windows.Clear();

        try
        {
            _standaloneWindow?.Close();
            _standaloneWindow = null;
        }
        catch { }
    }
}
