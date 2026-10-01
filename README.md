# deskbar-widget

Native-feel Windows virtual-desktops strip, pinned to the taskbar.

- Squircle number tiles (`1 2 3…`), click to switch, active tile follows the
  system accent. Light/dark follows Windows. Sits in the taskbar's free gap
  like a stock element (WS_CHILD docked into `Shell_TrayWnd` and `Shell_SecondaryTrayWnd` across multi-monitor displays).
- Stack: C# WPF on .NET 9, per-thread COM against the undocumented
  `IVirtualDesktopManagerInternal` family with tier probing + push
  notifications (translated blueprint: YASB Reborn, MIT). Taskbar anchoring
  adapted from EverythingToolbar (MIT). See `ATTRIBUTION.md`.
- Multi-monitor: Spawns instances across `Shell_SecondaryTrayWnd` so multi-monitor setups see the workspace strip on every display. Portable single-file `DeskbarWidget.exe`.

## Prerequisites

- Windows 10 or Windows 11
- [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
  or later (same model as EverythingToolbar, which requires the .NET 8
  Desktop Runtime)

## Install (portable)

1. Install the .NET Desktop Runtime above (once).
2. Download `DeskbarWidget.App.exe` from GitHub Releases (rename to
   `DeskbarWidget.exe` if you like).
3. Run it. On Windows 11 the strip docks into the taskbar automatically.
   If the taskbar refuses hosting, it falls back to a small standalone bar.
4. Autostart on startup: enabled by default. You can disable or enable it
   anytime via the system tray icon, or run `DeskbarWidget.exe --unregister-autostart`
   (re-enables with `--register-autostart`).

## Usage

- Click a desktop tile to switch desktops.
- Right-click any desktop tile to rename or delete it.
- Right-click the system tray icon to toggle "Show all desktop names", toggle "Autostart on startup", click "Open config folder", or exit.
- Configuration: Persisted as JSON in `~/.config/DeskbarWidget/config.json` (or `%APPDATA%\DeskbarWidget\config.json`, or overridden via `DESKBAR_CONFIG_HOME`). Supports hot-reloading: changes made to `config.json` take effect immediately on save.
- `--selfcheck`: prints Windows build, COM tier, autostart status, configuration file status, desktops + current, exits.
  Useful for bug reports on new Windows builds.

## Build

```powershell
dotnet build DeskbarWidget.sln -c Release
dotnet test DeskbarWidget.sln -c Release
.\scripts\publish-portable.ps1   # -> publish/DeskbarWidget.exe (win-x64 single file)
```

Requires .NET 9 SDK to build. The published exe is framework-dependent
(single small file, no runtime download at publish time); target machines
need only the .NET Desktop Runtime above. An Inno Setup installer that
checks/installs the runtime (like EverythingToolbar's `Installer/`) is a
planned follow-up, not v1.

## Docs

- `docs/ARCHITECTURE.md` — service/COM/taskbar/theme design + file map.
- `ATTRIBUTION.md` — upstream MIT notices.
