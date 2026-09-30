using DeskbarWidget.Core;
using DeskbarWidget.Interop;

namespace DeskbarWidget.Tests;

internal sealed class FakeReader : IDesktopReader
{
    public List<DesktopData> Desktops = [];
    public Guid CurrentGuid;
    public bool Fail;
    public List<Guid> SwitchedTo = [];
    public List<Guid> Removed = [];

    public IReadOnlyList<DesktopData> List()
    {
        if (Fail)
        {
            throw new VirtualDesktopException("shell down");
        }

        return Desktops;
    }

    public Guid Current()
    {
        if (Fail)
        {
            throw new VirtualDesktopException("shell down");
        }

        return CurrentGuid;
    }

    public void SwitchTo(Guid guid) => SwitchedTo.Add(guid);

    public void Remove(Guid guid)
    {
        Removed.Add(guid);
        var doomed = Desktops.FirstOrDefault(d => d.Guid == guid);
        if (doomed is not null)
        {
            Desktops.Remove(doomed);
            if (CurrentGuid == guid)
            {
                CurrentGuid = Desktops.FirstOrDefault()?.Guid ?? Guid.Empty;
            }
        }
    }
}

public sealed class VirtualDesktopServiceTests
{
    private static readonly Guid G1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid G2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid G3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static FakeReader TwoDesktops() => new()
    {
        Desktops =
        [
            new DesktopData(1, G1, "One"),
            new DesktopData(2, G2, "Two"),
        ],
        CurrentGuid = G1,
    };

    private static bool WaitFor(Func<bool> cond, int timeoutMs = 2000)
    {
        var end = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < end)
        {
            if (cond())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return cond();
    }

    [Fact]
    public void CurrentChanged_EmitsDesktopChanged_AndUpdatesCurrent()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        int? changed = null;
        svc.DesktopChanged += (_, i) => changed = i;

        fake.CurrentGuid = G2;
        svc.TestEmitComEvent(DesktopEvent.CurrentChanged);

        Assert.True(WaitFor(() => changed == 2));
        Assert.Equal(2, svc.GetCurrentDesktop().Number);
    }

    [Fact]
    public void Created_WithNewDesktop_EmitsDesktopsUpdated()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        DesktopsUpdatedEventArgs? updated = null;
        svc.DesktopsUpdated += (_, e) => updated = e;

        fake.Desktops.Add(new DesktopData(3, G3, "Three"));
        svc.TestEmitComEvent(DesktopEvent.Created);

        Assert.True(WaitFor(() => updated is not null));
        Assert.Equal(3, svc.GetDesktops().Count);
    }

    [Fact]
    public void Created_WithUnchangedState_EmitsNothing()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        var fired = false;
        svc.DesktopChanged += (_, _) => fired = true;
        svc.DesktopsUpdated += (_, _) => fired = true;

        svc.TestEmitComEvent(DesktopEvent.Created);
        Thread.Sleep(300);

        Assert.False(fired);
    }

    [Fact]
    public void Renamed_EmitsDesktopsUpdated_WithUpdateButtons()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        DesktopsUpdatedEventArgs? updated = null;
        svc.DesktopsUpdated += (_, e) => updated = e;

        svc.TestEmitComEvent(DesktopEvent.Renamed);

        Assert.True(WaitFor(() => updated is not null));
        Assert.True(updated!.UpdateButtons);
    }

    [Fact]
    public void SwitchDesktop_CallsReader_WithRightGuid_AndEmitsOptimistically()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        int? changed = null;
        svc.DesktopChanged += (_, i) => changed = i;

        svc.SwitchDesktop(2);

        Assert.Equal([G2], fake.SwitchedTo);
        Assert.Equal(2, changed);
    }

    [Fact]
    public void ShellFailure_DegradesToEmpty_AndPlaceholder()
    {
        var fake = TwoDesktops();
        fake.Fail = true;
        using var svc = new VirtualDesktopService(fake);

        Assert.Empty(svc.GetDesktops());
        Assert.Equal(0, svc.GetCurrentDesktop().Number);
        Assert.Equal(string.Empty, svc.GetDesktopName(1));
    }

    [Fact]
    public void Resolve_UnknownNumber_Throws()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);

        Assert.Throws<VirtualDesktopException>(() => svc.SwitchDesktop(9));
    }

    [Fact]
    public void DeleteDesktop_Removes_AndBroadcastsNewSet()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);
        DesktopsUpdatedEventArgs? updated = null;
        svc.DesktopsUpdated += (_, e) => updated = e;

        svc.DeleteDesktop(2);

        Assert.Equal([G2], fake.Removed);
        Assert.True(WaitFor(() => updated is not null));
        Assert.Single(svc.GetDesktops());
    }

    [Fact]
    public void DeleteDesktop_Current_FallsBackToRemaining()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);

        svc.DeleteDesktop(1);

        Assert.Equal([G1], fake.Removed);
        Assert.Equal(2, svc.GetCurrentDesktop().Number);
    }

    [Fact]
    public void DeleteDesktop_UnknownNumber_Throws()
    {
        var fake = TwoDesktops();
        using var svc = new VirtualDesktopService(fake);

        Assert.Throws<VirtualDesktopException>(() => svc.DeleteDesktop(9));
        Assert.Empty(fake.Removed);
    }
}
