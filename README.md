# Windows Dynamic Island

<div align="center">

[![Release](https://img.shields.io/badge/Release-v1.0.0--beta-6366f1?style=for-the-badge&logo=github)](https://github.com/paraxyoz/windows-dynamic-island/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078d4?style=for-the-badge&logo=windows)](https://github.com/paraxyoz/windows-dynamic-island)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20%7C%20WPF-512bd4?style=for-the-badge&logo=dotnet)](https://github.com/paraxyoz/windows-dynamic-island)
[![Performance](https://img.shields.io/badge/Performance-120%2B%20FPS%20GPU-10b981?style=for-the-badge&logo=nvidia)](https://github.com/paraxyoz/windows-dynamic-island)
[![Memory](https://img.shields.io/badge/Memory-~25%20MB%20RAM-059669?style=for-the-badge)](https://github.com/paraxyoz/windows-dynamic-island)
[![License](https://img.shields.io/badge/License-MIT-amber?style=for-the-badge)](LICENSE)

<br/>

**A lightweight, native, and high-performance Dynamic Island interface for Windows 10 and Windows 11.**

[Download Installer (.exe)](https://github.com/paraxyoz/windows-dynamic-island/releases/latest) | [Portable Version](https://github.com/paraxyoz/windows-dynamic-island/releases/latest) | [Features](#key-features) | [Hotkeys](#default-hotkeys) | [Build Guide](#building-from-source) | [Документация на русском](README.ru.md)

<br/>

<img src="assets/screenshots/capsule_dark.png" alt="Windows Dynamic Island in collapsed state" width="420" />

<br/>

<img src="assets/screenshots/home_dark.png" alt="Windows Dynamic Island Dark Theme" width="760" />

</div>

---

## Overview

**Windows Dynamic Island** is built using native **C# and WPF (.NET Framework 4.8)** with low-level Win32, Windows Runtime, and CoreAudio COM integrations:

- **Ultra-low Resource Footprint:** Consumes ~20–25 MB of RAM and 0% CPU/GPU cycles when idle.
- **Hardware-Accelerated Animation Engine:** Frame-locked 120 FPS+ spring physics without layout stutter.
- **True Click-Through Transparency (`HTTRANSPARENT`):** Only the active capsule consumes pointer events. Underlying windows remain fully clickable and scrollable.
- **Fullscreen Application Detection:** Automatically hides during full-screen games and media playback, scoped per-monitor.
- **Multi-Monitor & DPI Aware:** Dynamically positions and scales across multiple displays and varying display scaling factors.

---

## Visual Themes

<div align="center">

| Dark Theme (OLED Black) | Light Theme |
| :---: | :---: |
| <img src="assets/screenshots/home_dark.png" width="380" /> | <img src="assets/screenshots/home_light.png" width="380" /> |
| <img src="assets/screenshots/capsule_dark.png" width="380" /> | <img src="assets/screenshots/capsule_light.png" width="380" /> |

</div>

---

## Key Features

### 1. Collapsed State & Adaptive Meniscus
- **Seamless Drop Extrusion:** Attaches to the top edge of the primary or selected screen with cubic Bezier curves.
- **Companion Status Glyph:** Lightweight text-based kaomoji indicators (`^_^`, `•ᴗ•`, `[•_•]`, `=^..^=`, `◕‿◕`) reflecting audio state, layout toggles, and notification alerts.
- **Live Clock & Weather:** High-precision monospace clock display alongside real-time outdoor temperature.

### 2. Transient Notification Toasts
- **Preemptive Event Dispatching:** High-frequency actions (volume adjustments, media play/pause, track changes) preempt active toasts immediately without queuing delays.
- **Flicker-Free Hardware Volume:** Smooth quadratic easing on volume change notifications.
- **Notification Sounds:** Built-in clean audio presets (`Apple Pay`, `Plum`, `Trum`, `Phonk`) with an instant preview button in settings.
- **Interactive Action Buttons:** Note reminders can be marked completed or snoozed directly from the toast layer.

### 3. Dedicated Notes Workspace
- **Card-Based Note Management:** Structured vertical card list with live search, pin toggles, and timestamps.
- **One-Click Reminder Controls:** Quick toggles for reminders (`+5m`, `+15m`, `+1h`, and custom time entry `HH:mm`) with a direct disable action (`✕`).
- **Real-Time Auto-Save:** Changes are persisted atomically with automated recovery backups.

### 4. Privacy-Focused Clipboard History
- **Automatic History Tracking:** Captures text clipboard changes via `WM_CLIPBOARDUPDATE`.
- **Credential & Token Filter:** Automatically ignores sensitive data (passwords, tokens, API keys matching `sk-...`, `ghp_...`).
- **Native OLE Drag & Drop:** Drag clipboard items directly into applications, editors, Discord, or Telegram.

### 5. File Shelf
- **Non-Destructive Referencing:** Drag files onto the island to create temporary staging links without duplicating files on disk.
- **Cross-Application Drag & Drop:** Drag files directly from the shelf back into Explorer, chat clients, or browsers.

### 6. Time Instruments
- **Countdown Timer:** Quick duration presets (`1m`, `5m`, `10m`, `25m`) with live countdown projected into the collapsed capsule.
- **Stopwatch:** High-resolution split and cumulative lap tracking.

### 7. Weather Integration
- **Open-Meteo REST API:** Real-time meteorological data with zero API key requirement.
- **Forecasts:** 24-hour hourly trend and 7-day extended forecasts with offline caching.

---

## Default Hotkeys

| Key Combination | Action |
| :--- | :--- |
| `Alt + Space` | Expand / collapse Dynamic Island (or reveal if hidden) |
| `Alt + N` | Open Quick Note / Notes section |
| `Alt + B` | Open Clipboard History |

---

## Installation

### Installer (Recommended)
1. Download **`WindowsDynamicIslandSetup.exe`** from [Releases](https://github.com/paraxyoz/windows-dynamic-island/releases/latest).
2. Run the executable to install the application, create Start Menu shortcuts, and optionally register startup.
3. For silent automated deployment:
   ```powershell
   .\WindowsDynamicIslandSetup.exe --silent
   ```

### Portable Executable
1. Download **`WindowsDynamicIsland.exe`** from [Releases](https://github.com/paraxyoz/windows-dynamic-island/releases/latest).
2. Place the executable in any directory and launch directly. No registry changes or external dependencies are required.

---

## Building from Source

### Prerequisites
- Windows 10 (version 1903+) or Windows 11 (x64)
- .NET Framework 4.8 Developer Pack
- MSBuild (Visual Studio 2019/2022 or Build Tools)

### Build Steps
```powershell
# Clone the repository
git clone https://github.com/paraxyoz/windows-dynamic-island.git
cd windows-dynamic-island

# Compile binaries and execute unit tests
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build script will:
1. Compile `src/WindowsDynamicIsland` in `Release x64` configuration.
2. Run automated verification tests (`WindowsDynamicIsland.Tests.exe`).
3. Compile the standalone setup installer into `dist/`.

---

## Search Indexing & Tags

`windows dynamic island`, `dynamic island windows 11`, `dynamic island pc`, `wpf dynamic island`, `windows 11 desktop widget`, `csharp wpf desktop tool`, `desktop productivity utility`, `clipboard manager windows`, `windows 10 customization`.

---

## License

This project is licensed under the [MIT License](LICENSE).

Developed by [@paraxyoz](https://github.com/paraxyoz).
