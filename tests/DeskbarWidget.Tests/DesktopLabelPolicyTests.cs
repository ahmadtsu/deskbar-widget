using DeskbarWidget.Core;

namespace DeskbarWidget.Tests;

public sealed class DesktopLabelPolicyTests
{
    [Fact]
    public void GetLabel_WhenShowAllNamesFalse_AndActive_ReturnsName()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 2, name: "SC 2", isActive: true, showAllNames: false);
        Assert.Equal("SC 2", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesFalse_AndInactive_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 1, name: "Node js", isActive: false, showAllNames: false);
        Assert.Equal("1", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesFalse_AndActive_EmptyName_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 3, name: "", isActive: true, showAllNames: false);
        Assert.Equal("3", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesFalse_AndActive_WhitespaceName_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 3, name: "   ", isActive: true, showAllNames: false);
        Assert.Equal("3", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesFalse_AndActive_NullName_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 4, name: null, isActive: true, showAllNames: false);
        Assert.Equal("4", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesTrue_AndActive_ReturnsName()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 2, name: "SC 2", isActive: true, showAllNames: true);
        Assert.Equal("SC 2", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesTrue_AndInactive_ReturnsName()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 1, name: "Node js", isActive: false, showAllNames: true);
        Assert.Equal("Node js", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesTrue_AndUnnamed_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 5, name: null, isActive: false, showAllNames: true);
        Assert.Equal("5", label);
    }

    [Fact]
    public void GetLabel_WhenShowAllNamesTrue_AndWhitespace_ReturnsNumber()
    {
        var label = DesktopLabelPolicy.GetLabel(number: 5, name: "  ", isActive: false, showAllNames: true);
        Assert.Equal("5", label);
    }
}
