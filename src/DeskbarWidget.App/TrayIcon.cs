using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DeskbarWidget.App;

/// <summary>
/// System tray icon (WinForms NotifyIcon, no extra packages) using the
/// bundled eyes.ico. Right-click menu, v1: Exit only.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private Icon? _ownedIcon;
    private bool _disposed;

    public TrayIcon(Action onExit)
    {
        _ownedIcon = LoadIcon();

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => onExit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(exit);

        _icon = new Forms.NotifyIcon
        {
            Text = "DeskbarWidget",
            Icon = _ownedIcon,
            ContextMenuStrip = menu,
            Visible = false,
        };
    }

    public void Show() => _icon.Visible = true;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/eyes.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
            {
                DeskLogger.Info("tray: eyes.ico missing, using fallback icon");
                return SystemIcons.Application;
            }

            using (stream)
            {
                return new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            DeskLogger.Info($"tray: icon load failed ({ex.Message}), using fallback icon");
            return SystemIcons.Application;
        }
    }
}
