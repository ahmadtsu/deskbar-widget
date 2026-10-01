using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DeskbarWidget.App;

/// <summary>
/// System tray icon (WinForms NotifyIcon, no extra packages) using the
/// bundled eyes.ico. Right-click context menu supports toggling desktop names display,
/// toggling autostart on Windows startup, or exiting the application.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _showNamesItem;
    private readonly Forms.ToolStripMenuItem _autostartItem;
    private Icon? _ownedIcon;
    private bool _disposed;

    public TrayIcon(
        Action onExit,
        bool showAllNames,
        Action<bool> onShowAllNamesChanged,
        bool autostartEnabled = false,
        Action<bool>? onAutostartChanged = null,
        Func<bool>? getAutostartEnabled = null)
    {
        _ownedIcon = LoadIcon();

        _showNamesItem = new Forms.ToolStripMenuItem("Show all desktop names")
        {
            CheckOnClick = true,
            Checked = showAllNames,
            ToolTipText = "Show names for every desktop instead of only the active one",
        };
        _showNamesItem.Click += (_, _) => onShowAllNamesChanged(_showNamesItem.Checked);

        _autostartItem = new Forms.ToolStripMenuItem("Autostart on startup")
        {
            CheckOnClick = true,
            Checked = autostartEnabled,
            ToolTipText = "Automatically start DeskbarWidget when Windows logs in",
        };
        _autostartItem.Click += (_, _) => onAutostartChanged?.Invoke(_autostartItem.Checked);

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => onExit();

        var menu = new Forms.ContextMenuStrip
        {
            RenderMode = Forms.ToolStripRenderMode.System,
            ShowItemToolTips = true,
        };
        menu.Opening += (_, _) =>
        {
            if (getAutostartEnabled is not null)
            {
                _autostartItem.Checked = getAutostartEnabled();
            }
        };

        menu.Items.Add(_showNamesItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
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

    public void SetShowAllNames(bool showAllNames)
    {
        _showNamesItem.Checked = showAllNames;
    }

    public void SetAutostart(bool autostartEnabled)
    {
        _autostartItem.Checked = autostartEnabled;
    }

    internal Forms.ToolStripMenuItem AutostartMenuItem => _autostartItem;
    internal Forms.ToolStripMenuItem ShowNamesMenuItem => _showNamesItem;

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
            var uri = new Uri("pack://application:,,,/DeskbarWidget.App;component/eyes.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
            {
                var fallbackUri = new Uri("pack://application:,,,/eyes.ico", UriKind.Absolute);
                stream = System.Windows.Application.GetResourceStream(fallbackUri)?.Stream;
            }

            if (stream is null)
            {
                var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "eyes.ico");
                if (System.IO.File.Exists(filePath))
                {
                    return new Icon(filePath);
                }

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
