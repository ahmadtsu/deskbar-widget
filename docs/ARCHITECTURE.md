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
  fallback when the shell won't push, shell-unreachable tolerance. v1 ops:
  list / current / switch only.
- `src/DeskbarWidget.App` — WPF: invisible host + `StripWindow` (WS_CHILD of
  `Shell_TrayWnd`), free-gap anchor (UIA probe of tray icons + icon cluster),
  `DesktopsStripControl` squircle tiles, `ThemeWatcher` (Personalize registry
  + accent), `--selfcheck` / autostart switches.
- `tests/DeskbarWidget.Tests` — pure-logic tests (cache diff, label/tile
  state, tier-probe ordering with fakes). No live COM in tests.

## Runtime flow

1. App starts → `TierResolver` probes shell → `VirtualDesktopApi` enumerates
   once → service caches snapshot + current.
2. `DesktopNotificationListener` registers COM sink; shell pushes
   created/destroyed/moved/renamed/current-changed → service invalidates once
   per burst → UI diffs prev+curr tiles only.
3. `TaskbarAnchor` reparents `StripWindow` into the taskbar, measures the
   free gap (cluster right → next obstacle), `SetWindowPos` with glide;
   re-measures on layout events, DPI/display change, Explorer restart.
4. `ThemeWatcher` swaps Light/Dark tile tokens + accent fill live.

See `ATTRIBUTION.md` for upstream sources.
