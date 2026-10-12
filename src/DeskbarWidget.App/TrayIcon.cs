using System.Drawing;
using System.Windows;
using DeskbarWidget.Core;
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
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _showNamesItem;
    private readonly Forms.ToolStripMenuItem _autostartItem;
    private Forms.ToolStripMenuItem? _updateAvailableItem;
    private Forms.ToolStripSeparator? _updateAvailableSeparator;
    private Action? _onBalloonClick;
    private Icon? _ownedIcon;
    private bool _disposed;

    public TrayIcon(
        Action onExit,
        bool showAllNames,
        Action<bool> onShowAllNamesChanged,
        bool autostartEnabled = false,
        Action<bool>? onAutostartChanged = null,
        Func<bool>? getAutostartEnabled = null,
        Action? onOpenConfigFolder = null,
        Action? onReload = null,
        Action? onCheckForUpdates = null)
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

        var reloadItem = new Forms.ToolStripMenuItem("Reload DeskbarWidget")
        {
            ToolTipText = "Reload configuration, reconnect virtual desktop service, and re-dock widget",
        };
        reloadItem.Click += (_, _) => onReload?.Invoke();

        var openConfigItem = new Forms.ToolStripMenuItem("Open config folder")
        {
            ToolTipText = "Open the configuration folder in File Explorer",
        };
        openConfigItem.Click += (_, _) => onOpenConfigFolder?.Invoke();

        var checkUpdatesItem = new Forms.ToolStripMenuItem("Check for updates...")
        {
            ToolTipText = "Check GitHub for newer releases of DeskbarWidget",
        };
        checkUpdatesItem.Click += (_, _) => onCheckForUpdates?.Invoke();

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => onExit();

        _menu = new Forms.ContextMenuStrip
        {
            RenderMode = Forms.ToolStripRenderMode.System,
            ShowItemToolTips = true,
        };
        _menu.Opening += (_, _) =>
        {
            if (getAutostartEnabled is not null)
            {
                _autostartItem.Checked = getAutostartEnabled();
            }
        };

        _menu.Items.Add(_showNamesItem);
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        if (onReload is not null)
        {
            _menu.Items.Add(reloadItem);
        }
        if (onOpenConfigFolder is not null)
        {
            _menu.Items.Add(openConfigItem);
        }
        if (onCheckForUpdates is not null)
        {
            _menu.Items.Add(checkUpdatesItem);
        }
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(exit);

        _icon = new Forms.NotifyIcon
        {
            Text = "DeskbarWidget",
            Icon = _ownedIcon,
            ContextMenuStrip = _menu,
            Visible = false,
        };
        _icon.BalloonTipClicked += (_, _) =>
        {
            _onBalloonClick?.Invoke();
            _onBalloonClick = null;
        };
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            _icon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }
        catch
        {
            // Best effort; notifications may be disabled in OS settings.
        }
    }

    public void SetUpdateAvailable(UpdateCheckResult result, Action onOpenRelease)
    {
        ClearUpdateAvailable();

        _updateAvailableItem = new Forms.ToolStripMenuItem($"★ Update available (v{result.LatestVersion})...")
        {
            Font = new System.Drawing.Font(_menu.Font, System.Drawing.FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 204),
            ToolTipText = $"A newer version (v{result.LatestVersion}) is available on GitHub. Click to open release page.",
        };
        _updateAvailableItem.Click += (_, _) => onOpenRelease();

        _updateAvailableSeparator = new Forms.ToolStripSeparator();
        _menu.Items.Insert(0, _updateAvailableItem);
        _menu.Items.Insert(1, _updateAvailableSeparator);

        _onBalloonClick = onOpenRelease;
        ShowNotification(
            "DeskbarWidget Update Available",
            $"Version v{result.LatestVersion} is available on GitHub. Click to view release.");
    }

    public void ClearUpdateAvailable()
    {
        _onBalloonClick = null;
        if (_updateAvailableItem is not null)
        {
            _menu.Items.Remove(_updateAvailableItem);
            _updateAvailableItem.Dispose();
            _updateAvailableItem = null;
        }

        if (_updateAvailableSeparator is not null)
        {
            _menu.Items.Remove(_updateAvailableSeparator);
            _updateAvailableSeparator.Dispose();
            _updateAvailableSeparator = null;
        }
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
