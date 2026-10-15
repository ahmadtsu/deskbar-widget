using System.Windows;
using DeskbarWidget.App.Themes;
using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// Startup: CLI switches first (--selfcheck/--help/autostart), then the
/// taskbar-docked strip (or a standalone bar if docking fails).
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly AutostartService _autostartService = new();
    private VirtualDesktopService? _service;
    private ThemeWatcher? _watcher;
    private StripWindowManager? _windowManager;
    private TrayIcon? _tray;
    private UpdateService? _updateService;
    private System.Windows.Threading.DispatcherTimer? _updateTimer;
    private Mutex? _instanceMutex;
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            SetProcessDpiAwarenessContext((nint)DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
        }

        var args = e.Args.Select(a => a.ToLowerInvariant()).ToArray();
        if (args.Any(a => a is "--selfcheck" or "--help" or "-h" or "-?" or "/?"))
        {
            Shutdown(Selfcheck.Run(args.Contains("--selfcheck")));
            return;
        }

        if (args.Contains("--register-autostart") || args.Contains("--enable-autostart"))
        {
            Shutdown(RegisterAutostart());
            return;
        }

        if (args.Contains("--unregister-autostart") || args.Contains("--disable-autostart"))
        {
            Shutdown(UnregisterAutostart());
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        // Single instance: a second launch would double every tile and fight
        // over the taskbar gap (EverythingToolbar uses the same Mutex pattern).
        _instanceMutex = new Mutex(initiallyOwned: true, name: "DeskbarWidget", out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "DeskbarWidget is already running.",
                "DeskbarWidget",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // Default home is the left corner (left of the centered cluster);
        // pass --align=right to dock by the tray instead.
        var alignment = args.FirstOrDefault(a => a.StartsWith("--align="))?.Split('=')[1] == "right"
            ? "Right"
            : "Left";

        // Connect logger to ConfigService early
        ConfigService.Instance.Logger = DeskLogger.Info;
        ConfigService.Instance.ConfigChanged += OnExternalConfigChanged;

        var showAllNames = AppSettings.ShowAllDesktopNames;

        // Autostart on startup is the default behavior:
        // Register unless explicitly disabled by user in settings.
        if (AppSettings.AutostartEnabled)
        {
            _autostartService.Enable();
        }

        var autostartEnabled = _autostartService.CheckIsEnabled();
        _service = new VirtualDesktopService();
        if (ConfigService.Instance.CurrentConfig.LockedDesktops is { Count: > 0 } locked)
        {
            _service.LoadLockedDesktops(locked);
        }
        _service.LockedDesktopsChanged += (_, lockedGuids) =>
        {
            ConfigService.Instance.Update(cfg => cfg.LockedDesktops = lockedGuids.ToList());
        };

        _watcher = new ThemeWatcher();
        _windowManager = new StripWindowManager(_service, _watcher, alignment, showAllNames);

        DispatcherUnhandledException += (_, args) =>
        {
            DeskLogger.Info($"UI crash: {args.Exception}");
            System.Windows.MessageBox.Show(
                $"DeskbarWidget crashed:{Environment.NewLine}{args.Exception.Message}" +
                $"{Environment.NewLine}{Environment.NewLine}Details in %LocalAppData%\\DeskbarWidget\\deskbar.log",
                "DeskbarWidget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DeskLogger.Info($"Background crash: {args.ExceptionObject}");

        try
        {
            _updateService = new UpdateService(logger: DeskLogger.Info);

            _tray = new TrayIcon(
                onExit: Shutdown,
                showAllNames: showAllNames,
                onShowAllNamesChanged: OnShowAllNamesChanged,
                autostartEnabled: autostartEnabled,
                onAutostartChanged: OnAutostartChanged,
                getAutostartEnabled: () => _autostartService.CheckIsEnabled(),
                onOpenConfigFolder: OnOpenConfigFolder,
                onReload: OnReload,
                onCheckForUpdates: OnManualCheckForUpdates);

            _windowManager.Initialize();
            _service.Start();
            _tray.Show();
            DeskLogger.Info($"running (autostart={autostartEnabled}, config={AppSettings.ConfigFilePath})");

            if (ConfigService.Instance.CurrentConfig.CheckForUpdates)
            {
                ScheduleUpdateCheck(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            DeskLogger.Info($"Startup crash: {ex}");
            System.Windows.MessageBox.Show(
                $"DeskbarWidget failed to start:{Environment.NewLine}{ex.Message}" +
                $"{Environment.NewLine}{Environment.NewLine}Details in %LocalAppData%\\DeskbarWidget\\deskbar.log",
                "DeskbarWidget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnExternalConfigChanged(object? sender, DeskbarConfig config)
    {
        DeskLogger.Info($"config file reloaded (showAllNames={config.ShowAllDesktopNames}, autostart={config.AutostartEnabled})");
        Dispatcher.InvokeAsync(() =>
        {
            if (config.LockedDesktops is not null && _service is not null)
            {
                _service.LoadLockedDesktops(config.LockedDesktops);
                _service.ForceRefresh(updateButtons: true);
            }

            _windowManager?.SetShowAllNames(config.ShowAllDesktopNames);
            _tray?.SetShowAllNames(config.ShowAllDesktopNames);

            var currentAutostart = _autostartService.CheckIsEnabled();
            if (config.AutostartEnabled != currentAutostart)
            {
                _autostartService.SetAutostart(config.AutostartEnabled);
                _tray?.SetAutostart(_autostartService.CheckIsEnabled());
            }
        });
    }

    private void OnOpenConfigFolder()
    {
        try
        {
            var dir = AppSettings.ConfigDirectory;
            System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            DeskLogger.Info($"Failed to open config folder: {ex.Message}");
        }
    }

    private void OnShowAllNamesChanged(bool showAllNames)
    {
        DeskLogger.Info($"tray: toggle showAllNames={showAllNames}");
        AppSettings.ShowAllDesktopNames = showAllNames;
        _windowManager?.SetShowAllNames(showAllNames);
    }

    private void OnAutostartChanged(bool enabled)
    {
        DeskLogger.Info($"tray: toggle autostart={enabled}");
        AppSettings.AutostartEnabled = enabled;
        _tray?.SetAutostart(_autostartService.CheckIsEnabled());
    }

    private void OnReload()
    {
        DeskLogger.Info("tray: user clicked Reload DeskbarWidget");
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                ConfigService.Instance.Reload();
                _service?.Reconnect();
                _watcher?.Refresh();
                _windowManager?.SyncTaskbars();
                if (_windowManager != null)
                {
                    foreach (var window in _windowManager.Windows)
                    {
                        window.HandleDisplaySettingsChanged();
                    }
                }

                _tray?.ShowNotification("DeskbarWidget", "Reloaded successfully");
                DeskLogger.Info("tray: reload completed successfully");
            }
            catch (Exception ex)
            {
                DeskLogger.Info($"tray: reload failed: {ex.Message}");
            }
        });
    }

    private void ScheduleUpdateCheck(TimeSpan initialDelay)
    {
        _updateTimer?.Stop();
        _updateTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = initialDelay
        };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(24);
            await CheckForUpdatesAsync(isManual: false);
        };
        _updateTimer.Start();
    }

    private async void OnManualCheckForUpdates()
    {
        DeskLogger.Info("tray: user requested manual update check");
        try
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
            
            // Give brief visual feedback with wait cursor
            var minDelay = Task.Delay(400);
            var checkTask = CheckForUpdatesAsync(isManual: true);
            await Task.WhenAll(minDelay, checkTask);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
        }
    }

    private async Task CheckForUpdatesAsync(bool isManual)
    {
        if (_updateService is null)
        {
            return;
        }

        try
        {
            var config = ConfigService.Instance.CurrentConfig;
            if (!isManual && !config.CheckForUpdates)
            {
                return;
            }

            var result = await _updateService.CheckForUpdatesAsync(
                skippedVersion: config.SkippedUpdate,
                ignoreSkipped: isManual);

            Dispatcher.Invoke(() =>
            {
                var verDisplay = result.CurrentVersion.Build >= 0
                    ? result.CurrentVersion.ToString(3)
                    : result.CurrentVersion.ToString(2);

                if (result.IsUpdateAvailable)
                {
                    _tray?.SetUpdateAvailable(result, () => UpdateService.OpenReleaseUrl(result.ReleaseUrl));

                    if (isManual)
                    {
                        System.Windows.Input.Mouse.OverrideCursor = null;
                        System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                        var res = System.Windows.MessageBox.Show(
                            $"A new version of DeskbarWidget is available!{Environment.NewLine}{Environment.NewLine}" +
                            $"Current version: v{verDisplay}{Environment.NewLine}" +
                            $"Latest version: v{result.LatestVersion}{Environment.NewLine}{Environment.NewLine}" +
                            $"Would you like to open the GitHub release page to download it?",
                            "DeskbarWidget Update",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (res == MessageBoxResult.Yes)
                        {
                            UpdateService.OpenReleaseUrl(result.ReleaseUrl);
                        }
                    }
                }
                else if (isManual)
                {
                    System.Windows.Input.Mouse.OverrideCursor = null;
                    System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        System.Windows.MessageBox.Show(
                            $"Could not check for updates:{Environment.NewLine}{result.ErrorMessage}",
                            "DeskbarWidget Update",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            $"You are using the latest version of DeskbarWidget (v{verDisplay}).",
                            "DeskbarWidget Update",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            DeskLogger.Info($"Update check exception: {ex.Message}");
            if (isManual)
            {
                Dispatcher.Invoke(() =>
                {
                    System.Windows.Input.Mouse.OverrideCursor = null;
                    System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                    System.Windows.MessageBox.Show(
                        $"Could not check for updates:{Environment.NewLine}{ex.Message}",
                        "DeskbarWidget Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DeskLogger.Info($"OnExit code={e.ApplicationExitCode}");
        _updateTimer?.Stop();
        _updateService?.Dispose();
        ConfigService.Instance.ConfigChanged -= OnExternalConfigChanged;
        ConfigService.Instance.Dispose();
        _tray?.Dispose();
        _windowManager?.Dispose();
        _service?.Dispose();
        _watcher?.Dispose();
        base.OnExit(e);
    }

    private static int RegisterAutostart()
    {
        try
        {
            AppSettings.AutostartEnabled = true;
            if (new AutostartService().CheckIsEnabled())
            {
                Console.WriteLine("Autostart registered.");
                return 0;
            }

            Console.Error.WriteLine("Failed to register autostart.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to register autostart: {ex.Message}");
            return 1;
        }
    }

    private static int UnregisterAutostart()
    {
        try
        {
            AppSettings.AutostartEnabled = false;
            Console.WriteLine("Autostart removed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to remove autostart: {ex.Message}");
            return 1;
        }
    }
}
