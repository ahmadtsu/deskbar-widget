<p align="center">
  <img src="assets/eyes.png" width="64" height="64" alt="DeskbarWidget Logo" />
</p>

<h1 align="center">DeskbarWidget</h1>

<p align="center">
  <strong>Native-feel Windows virtual desktop pager docked seamlessly into the Windows taskbar.</strong>
</p>

<p align="center">
  <a href="https://github.com/ahmadtsu/deskbar-widget/releases"><img src="https://img.shields.io/github/v/release/ahmadtsu/deskbar-widget?include_prereleases&color=0078D4&style=flat-square" alt="Release"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-9.0%20Windows-512BD4?style=flat-square" alt=".NET 9.0">
  <img src="https://img.shields.io/badge/arch-x64-informational?style=flat-square" alt="Architecture">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License"></a>
</p>

<p align="center">
  <a href="#-features">Features</a> •
  <a href="#-installation">Installation</a> •
  <a href="#-controls--shortcuts">Controls & Shortcuts</a> •
  <a href="#%EF%B8%8F-configuration">Configuration</a> •
  <a href="#-building-from-source">Building</a> •
  <a href="#-attribution">Attribution</a>
</p>

<br/>

<p align="center">
  <img src="assets/DeskbarWidget.gif" alt="DeskbarWidget in action" width="85%" style="border-radius: 8px; box-shadow: 0 8px 30px rgba(0, 0, 0, 0.25);" />
</p>

<br/>

## 🌟 Features

* 🪟 **Seamless Taskbar Integration**: Docks directly as a native `WS_CHILD` window inside `Shell_TrayWnd`. Sits cleanly in the free gap of your taskbar alongside your pinned applications, feeling like a built-in OS feature.
* 🖥️ **Multi-Monitor Awareness**: Automatically spawns and synchronizes workspace strips across secondary displays (`Shell_SecondaryTrayWnd`). Switch or manage desktops from any connected monitor.
* 🎨 **Adaptive Windows Theming**: Follows Windows 10 and 11 Light and Dark themes dynamically. The active workspace pill automatically highlights with your personal Windows accent color.
* ⚡ **YASB-Style Dynamic Squircle Tiles**: Inactive desktops appear as compact squircle number tiles (`1`, `2`, `3`). The active desktop smoothly expands into an animated pill showing the workspace name with fluid width transitions.
* 🖱️ **Full Desktop Management**:
  * **Switch**: Click any tile to switch desktops instantly.
  * **Rename**: Right-click to open a sleek, theme-aware rename popup.
  * **Delete**: Right-click to remove empty or unused workspaces.
  * **Create**: Quick action to add a new virtual desktop on the fly.
* ⚙️ **Hot-Reloading JSON Configuration**: Configuration is stored in human-readable JSON (`config.json`) with active file-system watchers. Adjust alignment, appearance, or preferences without restarting the app.
* 🚀 **Zero-Overhead COM Pipeline**: Talks directly to undocumented Windows internal COM interfaces (`IVirtualDesktopManagerInternal`) with event-driven push sinks (`IVirtualDesktopNotification`). Never wastes CPU or battery cycles polling in the background.

---

## 📦 Installation

Choose the installation method that fits your workflow:

<details open>
<summary><strong>Option 1: Setup Installer (Recommended)</strong></summary>

<br/>

The Inno Setup installer delivers a zero-friction, user-friendly experience modeled after [EverythingToolbar](https://github.com/srwi/EverythingToolbar):

1. Download **`DeskbarWidget-Setup-x64.exe`** from [GitHub Releases](https://github.com/ahmadtsu/deskbar-widget/releases/latest).
2. Run the installer.
   * **Automatic Prerequisites**: Detects whether the **.NET Desktop Runtime 9.0** is present. If missing, it downloads and installs it silently for you.
   * **No Administrator Rights Required**: Installs per-user by default (`%LOCALAPPDATA%\Programs\DeskbarWidget`) without prompting for UAC elevation.
   * **Clean Process Handling**: Safely terminates and restarts existing instances without locking files or requiring reboots.
   * Adds an entry to the Windows Start Menu and optionally creates a Desktop icon.

</details>

<details>
<summary><strong>Option 2: Portable Single-File Executable</strong></summary>

<br/>

For users who prefer zero installation and portable setups:

1. Ensure [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (or later) is installed on your PC.
2. Download **`DeskbarWidget.exe`** from [GitHub Releases](https://github.com/ahmadtsu/deskbar-widget/releases/latest).
3. Place `DeskbarWidget.exe` in any folder of your choice and launch it.
4. Autostart can be enabled or disabled at any time via the system tray context menu.

</details>

---

## ⌨️ Controls & Shortcuts

| Action | Control | Description |
| :--- | :--- | :--- |
| **Switch Desktop** | <kbd>Left Click</kbd> on Tile | Instantly switches to the clicked virtual desktop. |
| **Workspace Menu** | <kbd>Right Click</kbd> on Tile | Opens menu to **Rename**, **Delete**, or **Create** a desktop. |
| **Create New Desktop** | <kbd>Right Click</kbd> on Strip background | Adds a new virtual desktop to your workspace list. |
| **System Tray Menu** | <kbd>Right Click</kbd> on Tray Icon | Access quick toggles, autostart options, and config folder. |

### CLI Switches

Run `DeskbarWidget.exe` from PowerShell or Command Prompt with optional flags:

```powershell
# Run system diagnostics and verify COM tier support
DeskbarWidget.exe --selfcheck

# Change strip docking alignment
DeskbarWidget.exe --align=left       # Dock left of centered taskbar icons (default)
DeskbarWidget.exe --align=right      # Dock right near the system tray

# Manage Windows logon startup registration
DeskbarWidget.exe --register-autostart
DeskbarWidget.exe --unregister-autostart
```

---

## ⚙️ Configuration

`DeskbarWidget` stores its preferences in a clean JSON format with **instant hot-reloading**. Any change saved to `config.json` immediately updates the active taskbar widget on screen.

### Config File Locations

The configuration file is resolved in the following priority order:
1. Custom environment path: `$env:DESKBAR_CONFIG_HOME\config.json`
2. User profile: `~/.config/DeskbarWidget/config.json`
3. Windows AppData: `%APPDATA%\DeskbarWidget\config.json`

> **Tip**: You can quickly open the configuration folder anytime by right-clicking the **DeskbarWidget** icon in the system tray and selecting **Open config folder**.

### Example `config.json`

```json
{
  "Alignment": "Left",
  "ShowAllDesktopNames": false,
  "AutostartEnabled": true
}
```

* **`Alignment`**: `"Left"` (places the widget to the left of centered taskbar icons) or `"Right"` (places the widget to the right near the system tray).
* **`ShowAllDesktopNames`**: `false` displays names only on the active expanding pill; `true` displays names across all desktop tiles.
* **`AutostartEnabled`**: `true` automatically runs DeskbarWidget when you log in to Windows.

---

## 🔍 Diagnostics (`--selfcheck`)

If you are experiencing issues on a new or preview Windows build (e.g. Windows Insider Canary/Dev), run the built-in self-check:

```powershell
.\DeskbarWidget.exe --selfcheck
```

Example diagnostic report:
```text
DeskbarWidget selfcheck:
  OS Build: 26100 (Windows 11 24H2)
  COM Tier: Win11_24H2 (IVirtualDesktopManagerInternal resolved)
  Notification Sink: Registered (Push notifications active)
  Monitors Detected: 2 (Primary + 1 Secondary)
  Config File: C:\Users\<Username>\AppData\Roaming\DeskbarWidget\config.json
  Autostart Registered: Yes (HKCU\Software\Microsoft\Windows\CurrentVersion\Run)
  Desktops [3]:
    [1] Main (Active)
    [2] Code
    [3] Browser
```
Copy and attach this output when submitting issues on GitHub to help us diagnose COM interface shifts.

---

## 🔨 Building from Source

### Prerequisites
* Windows 10 (1809+) or Windows 11
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
* [Inno Setup 6](https://jrsoftware.org/isdl.php) (optional, required only for building the installer)

### 1. Compile and Run Tests
```powershell
git clone https://github.com/ahmadtsu/deskbar-widget.git
cd deskbar-widget

# Build release binaries
dotnet build DeskbarWidget.sln -c Release

# Execute test suite (40+ unit & COM cache tests)
dotnet test DeskbarWidget.sln -c Release
```

### 2. Package Portable Executable
```powershell
.\scripts\publish-portable.ps1
# Generated single-file executable: publish\DeskbarWidget.exe
```

### 3. Package Inno Setup Installer
```powershell
.\scripts\build-installer.ps1
# Generated installer: installer\output\DeskbarWidget-Setup-x64.exe
```

---

## 🏛️ Architecture & Upstream Lineage

DeskbarWidget stands on the shoulders of two outstanding open-source projects:

* **[YASB (Yet Another Status Bar Reborn)](https://github.com/amnweb/yasb)**:
  * Inspired the animated squircle tile pager layout, active-workspace expansion pill (`label_workspace_active_btn`), and the multi-tier COM layout blueprint for `IVirtualDesktopManagerInternal`.
* **[EverythingToolbar](https://github.com/srwi/EverythingToolbar)**:
  * Inspired the robust Windows taskbar anchoring architecture (`WS_CHILD` reparenting into `Shell_TrayWnd`), layout gap measurement, and the low-friction Inno Setup installer workflow with automatic .NET runtime provisioning.

See [`ATTRIBUTION.md`](ATTRIBUTION.md) and [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for detailed architectural blueprints and licensing acknowledgments.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
