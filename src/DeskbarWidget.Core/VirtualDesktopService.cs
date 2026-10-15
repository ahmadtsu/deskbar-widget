// Translated from YASB `service.WindowsDesktopService` (MIT © 2024 amnweb).
// Snapshot cache + coalesced push notifications + poll fallback. v1 ops:
// list / current / switch. Shell-unreachable reads degrade to empty /
// placeholder so the strip can build and heal.

using System.Runtime.InteropServices;
using DeskbarWidget.Interop;

namespace DeskbarWidget.Core;

public sealed class VirtualDesktopService : IDisposable
{
    private const int PollIntervalMs = 500;

    private static readonly Lazy<VirtualDesktopService> SharedLazy = new(() => new VirtualDesktopService());
    public static VirtualDesktopService Shared => SharedLazy.Value;

    private readonly IDesktopReader _reader;
    private readonly Func<Action<DesktopEvent>, DesktopNotificationListener> _listenerFactory;
    private readonly SynchronizationContext? _context;
    private readonly object _gate = new();
    private DesktopNotificationListener? _listener;
    private Timer? _pollTimer;

    private List<DesktopInfo>? _desktops;
    private Guid? _currentGuid;

    private bool _pendingCurrent;
    private bool _pendingSet;
    private bool _pendingButtons;
    private bool _refreshQueued;

    private readonly HashSet<Guid> _lockedGuids = new();
    private Guid _lastAllowedGuid = Guid.Empty;
    private int _lastAllowedNumber = 1;
    private volatile bool _isBouncing;

    private int _lastCount;
    private int _lastNumber;
    private bool _disposed;

    public event EventHandler<int>? DesktopChanged;
    public event EventHandler<DesktopsUpdatedEventArgs>? DesktopsUpdated;
    public event EventHandler<IReadOnlySet<Guid>>? LockedDesktopsChanged;

    public VirtualDesktopService(IDesktopReader? reader = null, SynchronizationContext? context = null)
        : this(reader, context, listenerFactory: null)
    {
    }

    internal VirtualDesktopService(
        IDesktopReader? reader,
        SynchronizationContext? context,
        Func<Action<DesktopEvent>, DesktopNotificationListener>? listenerFactory)
    {
        _reader = reader ?? new VirtualDesktopApiAdapter(new VirtualDesktopApi());
        _listenerFactory = listenerFactory ?? (onEvent => new DesktopNotificationListener(onEvent));
        _context = context ?? SynchronizationContext.Current;

        try
        {
            var snapshot = Snapshot();
            _lastCount = snapshot.Count;
            _lastNumber = CurrentNumber();
            _lastAllowedGuid = CurrentGuid();
            _lastAllowedNumber = _lastNumber;
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            // Cold start while the shell is unreachable; heals on first refresh.
        }
    }

    public void Start()
    {
        _listener ??= _listenerFactory(OnComEvent);
        _listener.Start();

        _pollTimer ??= new Timer(_ => Poll(), null, PollIntervalMs, PollIntervalMs);
    }

    public void Stop()
    {
        _listener?.Stop();
        _listener = null;
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    public void Reconnect()
    {
        lock (_gate)
        {
            _desktops = null;
            _currentGuid = null;
            _refreshQueued = false;
            _pendingCurrent = false;
            _pendingSet = false;
            _pendingButtons = false;
        }

        _reader.Invalidate();
        _listener?.Restart();
        _pollTimer ??= new Timer(_ => Poll(), null, PollIntervalMs, PollIntervalMs);
        ForceRefresh(updateButtons: true);
    }

    public IReadOnlyList<DesktopInfo> GetDesktops()
    {
        try
        {
            return Snapshot();
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return [];
        }
    }

    public DesktopInfo GetCurrentDesktop()
    {
        try
        {
            return Resolve(CurrentNumber());
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return new DesktopInfo(0, Guid.Empty, string.Empty);
        }
    }

    public string GetDesktopName(int number)
    {
        try
        {
            return Resolve(number).Name;
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return string.Empty;
        }
    }

    public bool IsDesktopLocked(Guid guid)
    {
        lock (_gate)
        {
            return _lockedGuids.Contains(guid);
        }
    }

    public bool IsDesktopLocked(int number)
    {
        try
        {
            var desktop = Resolve(number);
            lock (_gate)
            {
                return _lockedGuids.Contains(desktop.Guid);
            }
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return false;
        }
    }

    public int GetUnlockedDesktopCount()
    {
        try
        {
            var desktops = Snapshot();
            lock (_gate)
            {
                return desktops.Count(d => !_lockedGuids.Contains(d.Guid));
            }
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return 0;
        }
    }

    public void LoadLockedDesktops(IEnumerable<Guid> guids)
    {
        lock (_gate)
        {
            _lockedGuids.Clear();
            foreach (var g in guids)
            {
                _lockedGuids.Add(g);
            }

            EnsureAtLeastOneUnlocked();
        }
    }

    public bool LockDesktop(int number)
    {
        DesktopInfo target;
        try
        {
            target = Resolve(number);
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return false;
        }

        lock (_gate)
        {
            var snapshot = Snapshot();
            var unlockedCount = snapshot.Count(d => !_lockedGuids.Contains(d.Guid));
            if (unlockedCount <= 1 && !_lockedGuids.Contains(target.Guid))
            {
                return false;
            }

            _lockedGuids.Add(target.Guid);
        }

        NotifyLockedChanged();

        try
        {
            var currentGuid = CurrentGuid();
            if (currentGuid == target.Guid)
            {
                var nextUnlocked = Snapshot().FirstOrDefault(d => !_lockedGuids.Contains(d.Guid));
                if (nextUnlocked is not null)
                {
                    SwitchDesktop(nextUnlocked.Number);
                    return true;
                }
            }
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
        }

        ForceRefresh(updateButtons: true);
        return true;
    }

    public bool UnlockDesktop(int number)
    {
        DesktopInfo target;
        try
        {
            target = Resolve(number);
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return false;
        }

        bool removed;
        lock (_gate)
        {
            removed = _lockedGuids.Remove(target.Guid);
        }

        if (removed)
        {
            NotifyLockedChanged();
            ForceRefresh(updateButtons: true);
        }

        return removed;
    }

    public void SwitchDesktop(int number)
    {
        var desktop = Resolve(number);
        lock (_gate)
        {
            if (_lockedGuids.Contains(desktop.Guid))
            {
                return;
            }
        }

        try
        {
            _reader.SwitchTo(desktop.Guid);
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            _reader.Invalidate();
            _reader.SwitchTo(desktop.Guid);
        }

        lock (_gate)
        {
            _desktops = null;
            _currentGuid = null;
        }

        // Optimistic emit; the shell notification confirms it right after.
        DesktopChanged?.Invoke(this, number);
    }

    /// <summary>
    /// Delete a desktop (YASB service.remove_desktop). The shell moves to the
    /// fallback desktop when the deleted one was in view; the following
    /// refresh broadcasts the new set, and selection changes surface through
    /// the normal notification path.
    /// </summary>
    public void DeleteDesktop(int number)
    {
        var desktop = Resolve(number);
        _reader.Remove(desktop.Guid);
        lock (_gate)
        {
            if (_lockedGuids.Remove(desktop.Guid))
            {
                NotifyLockedChanged();
            }
        }
        ForceRefresh(updateButtons: false);
    }

    /// <summary>
    /// Rename a desktop (YASB service.rename_desktop). Invalidates cache and
    /// broadcasts the updated desktop set with updateButtons=true so tile
    /// labels and active pill widths recompute immediately.
    /// </summary>
    public void RenameDesktop(int number, string name)
    {
        var desktop = Resolve(number);
        _reader.Rename(desktop.Guid, name);
        ForceRefresh(updateButtons: true);
    }

    /// <summary>
    /// Create a new desktop (YASB service.create_desktop). Invalidates cache and
    /// broadcasts the updated desktop set so the new tile appears immediately.
    /// </summary>
    public Guid CreateDesktop()
    {
        var guid = _reader.Create();
        ForceRefresh(updateButtons: false);
        return guid;
    }

    public void ForceRefresh(bool updateButtons = false)
    {
        lock (_gate)
        {
            _desktops = null;
            _currentGuid = null;
        }

        int number;
        int count;
        try
        {
            number = CurrentNumber();
            count = Snapshot().Count;
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return;
        }

        _lastCount = count;
        _lastNumber = number;
        DesktopsUpdated?.Invoke(this, new DesktopsUpdatedEventArgs(number, updateButtons));
    }

    // ---- internals (test seam: feed events without COM) ----

    internal void TestEmitComEvent(DesktopEvent e) => OnComEvent(e);

    private void OnComEvent(DesktopEvent e)
    {
        lock (_gate)
        {
            switch (e)
            {
                case DesktopEvent.CurrentChanged:
                    _pendingCurrent = true;
                    break;
                case DesktopEvent.Created:
                case DesktopEvent.Destroyed:
                case DesktopEvent.Moved:
                    _pendingSet = true;
                    break;
                case DesktopEvent.Renamed:
                    _pendingSet = true;
                    _pendingButtons = true;
                    break;
                default:
                    return;
            }

            if (_refreshQueued)
            {
                return;
            }

            _refreshQueued = true;
        }

        if (_context is not null)
        {
            _context.Post(_ => ApplyPending(), null);
        }
        else
        {
            ThreadPool.QueueUserWorkItem(_ => ApplyPending());
        }
    }

    private void ApplyPending()
    {
        bool current;
        bool set;
        bool buttons;
        lock (_gate)
        {
            _refreshQueued = false;
            current = _pendingCurrent;
            set = _pendingSet;
            buttons = _pendingButtons;
            _pendingCurrent = _pendingSet = _pendingButtons = false;
            _desktops = null;
            _currentGuid = null;
        }

        int number;
        Guid guid;
        try
        {
            guid = CurrentGuid();
            number = CurrentNumber();
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return;
        }

        if (set)
        {
            CleanupDeadLockedGuids();
        }

        lock (_gate)
        {
            if (_lockedGuids.Contains(guid))
            {
                BounceAwayFromLockedDesktop();
                return;
            }

            _lastAllowedGuid = guid;
            _lastAllowedNumber = number;
        }

        if (current)
        {
            _lastNumber = number;
            DesktopChanged?.Invoke(this, number);
        }

        if (current || set)
        {
            RefreshState(buttons);
        }
    }

    private void RefreshState(bool updateButtons)
    {
        int count;
        int number;
        try
        {
            count = Snapshot().Count;
            number = CurrentNumber();
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            return;
        }

        if (count != _lastCount || number != _lastNumber || updateButtons)
        {
            _lastCount = count;
            _lastNumber = number;
            DesktopsUpdated?.Invoke(this, new DesktopsUpdatedEventArgs(number, updateButtons));
        }
    }

    private void Poll()
    {
        lock (_gate)
        {
            _currentGuid = null;
        }

        int number;
        Guid guid;
        try
        {
            guid = CurrentGuid();
            number = CurrentNumber();
        }
        catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
        {
            _reader.Invalidate();
            return;
        }

        if (_listener is not null && !_listener.Active)
        {
            _listener.Start();
        }

        lock (_gate)
        {
            if (_lockedGuids.Contains(guid))
            {
                BounceAwayFromLockedDesktop();
                return;
            }

            _lastAllowedGuid = guid;
            _lastAllowedNumber = number;
        }

        if (number != _lastNumber)
        {
            _lastNumber = number;
            DesktopChanged?.Invoke(this, number);
        }

        RefreshState(false);
    }

    private void BounceAwayFromLockedDesktop()
    {
        if (_isBouncing)
        {
            return;
        }

        _isBouncing = true;
        try
        {
            Guid targetGuid = Guid.Empty;
            var snapshot = Snapshot();

            if (_lastAllowedGuid != Guid.Empty &&
                !_lockedGuids.Contains(_lastAllowedGuid) &&
                snapshot.Any(d => d.Guid == _lastAllowedGuid))
            {
                targetGuid = _lastAllowedGuid;
            }
            else
            {
                var fallback = snapshot.FirstOrDefault(d => !_lockedGuids.Contains(d.Guid));
                if (fallback is not null)
                {
                    targetGuid = fallback.Guid;
                }
            }

            if (targetGuid != Guid.Empty)
            {
                try
                {
                    _reader.SwitchTo(targetGuid);
                }
                catch (Exception ex) when (ShellErrorFilter.IsShellFailure(ex))
                {
                    _reader.Invalidate();
                    _reader.SwitchTo(targetGuid);
                }

                lock (_gate)
                {
                    _desktops = null;
                    _currentGuid = targetGuid;
                }
            }
        }
        catch
        {
        }
        finally
        {
            _isBouncing = false;
        }
    }

    private void NotifyLockedChanged()
    {
        HashSet<Guid> copy;
        lock (_gate)
        {
            copy = new HashSet<Guid>(_lockedGuids);
        }

        LockedDesktopsChanged?.Invoke(this, copy);
    }

    private void EnsureAtLeastOneUnlocked()
    {
        try
        {
            var snapshot = Snapshot();
            if (snapshot.Count > 0 && snapshot.All(d => _lockedGuids.Contains(d.Guid)))
            {
                var currentGuid = CurrentGuid();
                if (!_lockedGuids.Remove(currentGuid) && snapshot.Count > 0)
                {
                    _lockedGuids.Remove(snapshot[0].Guid);
                }
            }
        }
        catch
        {
        }
    }

    private void CleanupDeadLockedGuids()
    {
        try
        {
            var validGuids = Snapshot().Select(d => d.Guid).ToHashSet();
            bool changed;
            lock (_gate)
            {
                changed = _lockedGuids.RemoveWhere(g => !validGuids.Contains(g)) > 0;
            }

            if (changed)
            {
                NotifyLockedChanged();
            }
        }
        catch
        {
        }
    }

    private List<DesktopInfo> Snapshot()
    {
        if (_desktops is null)
        {
            _desktops = _reader.List()
                .Select(d => new DesktopInfo(d.Number, d.Guid, d.Name))
                .ToList();
        }

        return _desktops;
    }

    private Guid CurrentGuid()
    {
        _currentGuid ??= _reader.Current();
        return _currentGuid.Value;
    }

    private int CurrentNumber()
    {
        var guid = CurrentGuid();
        var number = Snapshot().FirstOrDefault(d => d.Guid == guid)?.Number ?? 0;
        if (number == 0)
        {
            lock (_gate)
            {
                _desktops = null;
            }

            number = Snapshot().FirstOrDefault(d => d.Guid == guid)?.Number ?? 0;
            if (number == 0)
            {
                throw new VirtualDesktopException("Current desktop is not in the desktop list.");
            }
        }

        return number;
    }

    private DesktopInfo Resolve(int number)
    {
        var desktop = Snapshot().FirstOrDefault(d => d.Number == number);
        if (desktop is null)
        {
            lock (_gate)
            {
                _desktops = null;
                _currentGuid = null;
            }

            desktop = Snapshot().FirstOrDefault(d => d.Number == number);
        }

        return desktop ?? throw new VirtualDesktopException($"No desktop at position {number}.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}

// Only shell/COM failures reach these filters: the API throws
// VirtualDesktopException/COMException/UnauthorizedAccessException, so
// unrelated bugs (ArgumentNull etc.) still propagate instead of being
// swallowed as "shell unavailable".
internal static class ShellErrorFilter
{
    public static bool IsShellFailure(Exception ex) =>
        ex is VirtualDesktopException
            or COMException
            or UnauthorizedAccessException
            or InvalidComObjectException;
}
