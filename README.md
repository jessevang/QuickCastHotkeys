# QuickCastHotkeys with Linux Click Support

A Stardew Valley mod that adds hotkeys for smart casting tools—with full support for Linux (including Steam Deck) via bundled xdotool.

---

## Features

- **Smart Cast Mode**: Supports two modes
  - **Windows**: Uses native `mouse_event()` calls.
  - **Linux**: Calls bundled `xdotool` to simulate left-clicks.
- Automatically skips manual installation of `xdotool`. No root required.

---

## Included Files

In `mods/QuickCastHotkeys/`:
- `QuickCastHotkeys.dll` — The mod itself.
- `xdotool` — Precompiled 64-bit executable (v3.20210903.1).
- `LICENSE.xdotool.txt` — BSD 3‑Clause license for xdotool.

---

## Setup Instructions for Linux Users

1. **Ensure execute permission** is set (run once):
   ```bash
   cd mods/QuickCastHotkeys
   chmod +x xdotool
