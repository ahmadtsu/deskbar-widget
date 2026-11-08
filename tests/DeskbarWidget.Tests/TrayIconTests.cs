using DeskbarWidget.App;
using Xunit;

namespace DeskbarWidget.Tests;

public sealed class TrayIconTests
{
    [Fact]
    public void TrayIcon_HideLockedDesktopsMenuItem_DefaultsAndProperties()
    {
        using var tray = new TrayIcon(
            onExit: () => { },
            showAllNames: false,
            onShowAllNamesChanged: _ => { },
            hideLockedDesktops: false);

        var item = tray.HideLockedDesktopsMenuItem;
        Assert.NotNull(item);
        Assert.Equal("Hide locked desktops", item.Text);
        Assert.True(item.CheckOnClick);
        Assert.False(item.Checked);
    }

    [Fact]
    public void TrayIcon_HideLockedDesktopsMenuItem_InitializesChecked_WhenTrue()
    {
        using var tray = new TrayIcon(
            onExit: () => { },
            showAllNames: false,
            onShowAllNamesChanged: _ => { },
            hideLockedDesktops: true);

        Assert.True(tray.HideLockedDesktopsMenuItem.Checked);
    }

    [Fact]
    public void TrayIcon_SetHideLockedDesktops_UpdatesMenuItem()
    {
        using var tray = new TrayIcon(
            onExit: () => { },
            showAllNames: false,
            onShowAllNamesChanged: _ => { },
            hideLockedDesktops: false);

        Assert.False(tray.HideLockedDesktopsMenuItem.Checked);

        tray.SetHideLockedDesktops(true);
        Assert.True(tray.HideLockedDesktopsMenuItem.Checked);

        tray.SetHideLockedDesktops(false);
        Assert.False(tray.HideLockedDesktopsMenuItem.Checked);
    }

    [Fact]
    public void TrayIcon_HideLockedDesktopsMenuItem_Click_TriggersCallback()
    {
        bool? receivedValue = null;
        using var tray = new TrayIcon(
            onExit: () => { },
            showAllNames: false,
            onShowAllNamesChanged: _ => { },
            hideLockedDesktops: false,
            onHideLockedDesktopsChanged: val => receivedValue = val);

        tray.HideLockedDesktopsMenuItem.PerformClick();

        Assert.True(tray.HideLockedDesktopsMenuItem.Checked);
        Assert.True(receivedValue);

        tray.HideLockedDesktopsMenuItem.PerformClick();

        Assert.False(tray.HideLockedDesktopsMenuItem.Checked);
        Assert.False(receivedValue);
    }
}
