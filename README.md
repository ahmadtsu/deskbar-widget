# deskbar-widget

Native-feel Windows virtual-desktops strip, pinned to the taskbar.

- Squircle number tiles (`1 2 3…`), click to switch, active tile follows the
  system accent. Light/dark follows Windows. Sits in the taskbar's free gap
  like a stock element (WS_CHILD docked into `Shell_TrayWnd`).
- Stack: C# WPF on .NET 9, per-thread COM against the undocumented
  `IVirtualDesktopManagerInternal` family with tier probing + push
  notifications (translated blueprint: YASB Reborn, MIT). Taskbar anchoring
  adapted from EverythingToolbar (MIT). See `ATTRIBUTION.md`.
- v1 scope: switch + active only. Portable single-file `DeskbarWidget.exe`.

## Install (portable)

1. Download `DeskbarWidget.exe` from GitHub Releases.
2. Run it. On Windows 11 the strip docks into the taskbar automatically.
   If the taskbar refuses hosting, it falls back to a small standalone bar.
3. Optional autostart: run `DeskbarWidget.exe --register-autostart`
   (removes with `--unregister-autostart`).

## Usage

- Click a number tile to switch desktops.
- `--selfcheck`: prints Windows build, COM tier, desktops + current, exits.
  Useful for bug reports on new Windows builds.

## Build

```powershell
dotnet build DeskbarWidget.sln -c Release
dotnet test DeskbarWidget.sln -c Release
.\scripts\publish-portable.ps1   # -> publish/DeskbarWidget.exe (win-x64 single file)
```

Requires .NET 9 SDK. No runtime needed on the target machine for the
self-contained portable exe.

## Docs

- `docs/ARCHITECTURE.md` — service/COM/taskbar/theme design + file map.
- `ATTRIBUTION.md` — upstream MIT notices.
