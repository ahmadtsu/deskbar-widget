using System.Windows;
using DeskbarWidget.Core;

namespace DeskbarWidget.App;

/// <summary>
/// Interaction logic for App.xaml. Handles --selfcheck / --help before any
/// window shows; the taskbar strip (Phase 2/3) replaces MainWindow later.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;
        if (args.Any(a => a is "--selfcheck" or "--help" or "-h" or "-?"))
        {
            var code = Selfcheck.Run(args.Contains("--selfcheck"));
            Shutdown(code);
            return;
        }

        base.OnStartup(e);
    }
}

