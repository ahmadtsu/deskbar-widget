// Port for desktop reads. The production adapter wraps Interop.VirtualDesktopApi;
// tests inject fakes. Keeps Core unit-testable without live COM.

using DeskbarWidget.Interop;

namespace DeskbarWidget.Core;

public interface IDesktopReader
{
    IReadOnlyList<DesktopData> List();
    Guid Current();
    void SwitchTo(Guid guid);
    void Remove(Guid guid);
}

public sealed class VirtualDesktopApiAdapter(VirtualDesktopApi api) : IDesktopReader
{
    public IReadOnlyList<DesktopData> List() => api.ListDesktops();
    public Guid Current() => api.CurrentGuid();
    public void SwitchTo(Guid guid) => api.SwitchTo(guid);
    public void Remove(Guid guid) => api.Remove(guid);
}
