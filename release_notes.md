# Windows Dynamic Island — v1.0.0-beta

Public beta release of **Windows Dynamic Island** — a lightweight, native, and high-performance Dynamic Island utility for Windows 10 and Windows 11.

---

### Downloads

- **`WindowsDynamicIslandSetup.exe`** — Complete standalone installer providing the application, sound presets, desktop & start menu shortcuts, and autostart registration out-of-the-box.

---

### Highlights

#### Brand Icon & Visual Themes
- **New Squircle Visual Identity:** Custom glowing squircle icon applied to the application, setup installer, taskbar tray, and desktop shortcuts.
- **Adaptive Meniscus:** Seamless drop extrusion attaching to the top screen edge with cubic Bezier curves and corner smoothing.
- **Two Visual Themes:** OLED Dark Theme and Clean Light Theme.
- **Hardware-Accelerated Physics:** 120 FPS+ spring animation engine with smooth transitions between collapsed, toast, and expanded workspaces.
- **Click-Through Transparency (`HTTRANSPARENT`):** Unoccupied window regions pass mouse clicks through to desktop and underlying applications.

#### Dedicated Notes Workspace
- **Card-Based List:** Each note is displayed as an individual card with real-time search, pinning, and modification timestamps.
- **One-Click Reminder Toggles:** Fast reminder cancellation directly in the list view (`X`) or reminder adjustments (`+5m`, `+15m`, `+1h`, `HH:mm`) in the editor.
- **Automatic Recovery:** Atomic disk persistence with backup redundancy.

#### Modern Notification Sounds (Out-of-the-Box)
- Built-in audio presets: **Apple Pay**, **Plum**, **Trum**, and **Phonk** (Apple Pay set as default).
- Instant preview playback directly in **Settings**.

#### Media & System Notifications
- High-frequency events (volume adjustments, media control) preempt active toasts without queue buildup.
- Flicker-free volume slider indicator with smooth easing.
- Calm text-based companion glyph (`^_^`) during media playback.

#### Privacy-Focused Clipboard & File Shelf
- Automatic filtering of credentials, tokens, and API keys.
- Native OLE Drag & Drop: drag copied entries or files directly into applications, chats, or browsers.

---

### Default Hotkeys

- `Alt + Space` — Expand / collapse Dynamic Island.
- `Alt + N` — Open Quick Notes workspace.
- `Alt + B` — Open Clipboard History workspace.

---

### System Requirements

- **OS:** Windows 10 (1903+) or Windows 11 (x64).
- **Runtime:** .NET Framework 4.8 (pre-installed on Windows 10/11).
- **Footprint:** ~25 MB RAM, 0% CPU at idle.
