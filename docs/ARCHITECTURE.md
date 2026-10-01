# Architecture

## Pieces

- `src/DeskbarWidget.Interop` — hand-written COM definitions for the
  undocumented virtual-desktop API (translated from YASB `interfaces.py` /
  `com.py`): CLSIDs, per-tier `IVirtualDesktopManagerInternal` layouts,
  notification sink interfaces, tier probing (newest→oldest GUID probe, not
  build-number only). No COM pointers escape above this layer.
- `src/DeskbarWidget.Core` — `VirtualDesktopService`: cached
  `DesktopInfo{Number,Guid,Name}` snapshot + current GUID, `desktop_changed` /
  `desktops_updated` events, coalesced notification batches, 500 ms poll
  fallback when the shell won't push, shell-unreachable tolerance. `AutostartService`:
  encapsulates `HKCU\...\CurrentVersion\Run` registration, quoted paths, and existence
  verification. v1 ops: list / current / switch only.
- `src/DeskbarWidget.App` — WPF: invisible host + `StripWindowManager`
  managing `StripWindow` instances (`WS_CHILD` docked into `Shell_TrayWnd` and
  `Shell_SecondaryTrayWnd` across multi-monitor setups), free-gap anchor (UIA
  probe of tray icons + icon cluster), `DesktopsStrip` squircle tiles,
  `ThemeWatcher` (Personalize registry + accent), `TrayIcon` (system tray menu
  with desktop names and autostart toggles), `--selfcheck` / autostart switches.
- `tests/DeskbarWidget.Tests` — pure-logic tests (cache diff, label/tile
  state, tier-probe ordering with fakes, multi-monitor taskbar bounds). No live COM in tests.

## Runtime flow

1. App starts → `TierResolver` probes shell → `VirtualDesktopApi` enumerates
   once → service caches snapshot + current.
2. `DesktopNotificationListener` registers COM sink; shell pushes
   created/destroyed/moved/renamed/current-changed → service invalidates once
   per burst → UI diffs prev+curr tiles only.
3. `StripWindowManager` discovers all taskbars (`Shell_TrayWnd` + `Shell_SecondaryTrayWnd`)
   and docks `StripWindow` instances into each; measures the free gap (cluster right → next obstacle),
   `SetWindowPos` with glide; re-measures on layout events, DPI/display change, Explorer restart.
4. `ThemeWatcher` swaps Light/Dark tile tokens + accent fill live.

See `ATTRIBUTION.md` for upstream sources.
