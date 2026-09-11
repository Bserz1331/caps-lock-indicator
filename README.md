# Caps Lock Indicator

A portable Windows `.exe` utility that shows whether Caps Lock is enabled. It runs in the notification area and does not require PowerShell or the .NET SDK.

## Which file should I open?

- To use the utility, double-click `CapsLockIndicator.exe`.
- To launch it from a clearly named shortcut script, double-click `Start Caps Lock Indicator.bat`.
- Traditional Chinese instructions are available in `README.zh-TW.md`.
- The other files are source code, scripts, and image assets.

## Features

- Runs quietly in the Windows notification area.
- The tray icon shows a large `A` on a green keycap when Caps Lock is on, and a lowercase `a` on a gray keycap when it is off.
- Pressing Caps Lock shows a centered status overlay on the active monitor.
- The overlay stays on top, lets mouse clicks pass through, and hides automatically after the configured duration.
- The default overlay is 80% transparent (about 20% window opacity) and remains visible enough to find.
- Right-click the tray icon and choose `Overlay settings...` to customize the on/off colors, transparency, and display duration from 1 to 5 seconds.
- Right-click the tray icon and open `Language` to choose `System default`, `Traditional Chinese`, or `English`. The default follows the Windows display language.
- Right-click the tray icon and choose `Check for updates` to read version information from GitHub. The utility never downloads or installs updates automatically.
- Right-click the tray icon and enable `Start with Windows` to launch it automatically after the current Windows user signs in. This setting does not require administrator permission.
- Right-click the tray icon and choose `Support development...` to open the voluntary support options.

## Folder layout

```text
CapsLock/
├─ CapsLockIndicator.exe              <- Main application
├─ Start Caps Lock Indicator.bat      <- English launcher
├─ 啟動 Caps Lock 指示器.bat          <- Original launcher
├─ README.md                           <- English documentation
├─ README.zh-TW.md                     <- Traditional Chinese documentation
├─ version.json                        <- Update-check manifest
├─ src/CapsLockIndicator.cs            <- Source code
├─ scripts/CapsLockIndicator.ps1       <- PowerShell-compatible launcher
└─ assets/                             <- Icon and preview image
```

## Notes

If Windows shows a SmartScreen warning, select `More info` and then `Run anyway`. This is a common warning for an unsigned local utility.
