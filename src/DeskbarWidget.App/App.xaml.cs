using System.Windows;
using DeskbarWidget.App.Themes;
using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.App;

/// <summary>
/// Startup: CLI switches first (--selfcheck/--help/autostart), then the
/// taskbar-docked strip (or a standalone bar if docking fails).
/// </summary>
public partial class App : System.Windows.Application
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "DeskbarWidget";

    private VirtualDesktopService? _service;
    private ThemeWatcher? _watcher;
    private StripWindow? _window;
    private TrayIcon? _tray;
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        var args = e.Args.Select(a => a.ToLowerInvariant()).ToArray();
        if (args.Any(a => a is "--selfcheck" or "--help" or "-h" or "-?" or "/?"))
        {
            Shutdown(Selfcheck.Run(args.Contains("--selfcheck")));
            return;
        }

        if (args.Contains("--register-autostart"))
        {
            Shutdown(RegisterAutostart());
            return;
        }

        if (args.Contains("--unregister-autostart"))
        {
            Shutdown(UnregisterAutostart());
            return;
        }

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

        _service = new VirtualDesktopService();
        _watcher = new ThemeWatcher();
        _window = new StripWindow(_service, _watcher, alignment);

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
            _tray = new TrayIcon(Shutdown);
            _window.Initialize();
            _service.Start();
            _tray.Show();
            DeskLogger.Info("running");
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

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _service?.Dispose();
        _watcher?.Dispose();
        base.OnExit(e);
    }

    private static int RegisterAutostart()
    {
        try
        {
            var exe = Environment.ProcessPath ?? string.Empty;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey);
            key?.SetValue(RunValue, $"\"{exe}\"");
            Console.WriteLine("Autostart registered.");
            return 0;
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
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(RunValue, throwOnMissingValue: false);
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
