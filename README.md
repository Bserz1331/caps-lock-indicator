# Caps Lock Indicator

<p align="center">
  <img src="assets/CapsLockIndicator.png" alt="Caps Lock Indicator icon" width="96">
</p>

<p align="center">A portable Windows utility that shows the current Caps Lock state in the notification area and on screen.</p>

<p align="center">
  <a href="https://raw.githubusercontent.com/Bserz1331/caps-lock-indicator/main/CapsLockIndicator.exe">Download v1.3.0</a> ·
  <a href="README.zh-TW.md">繁體中文</a> ·
  <a href="src/CapsLockIndicator.cs">Source code</a>
</p>

Caps Lock Indicator places a small `A` or `a` indicator in the Windows notification area. When the Caps Lock state changes, it also shows a short status overlay in the center of the monitor that is currently being used.

It only reads the current Windows Caps Lock state. It does not remap keys, change keyboard input, or run a background service.

## Download and get started

1. Download the current [`CapsLockIndicator.exe`](https://raw.githubusercontent.com/Bserz1331/caps-lock-indicator/main/CapsLockIndicator.exe) from this repository.
2. Save it in a permanent folder. No installer is required.
3. Double-click `CapsLockIndicator.exe` to start it.
4. If you prefer a named launcher, use `Start Caps Lock Indicator.bat`; the original Chinese launcher is also included.
5. Look in the Windows notification area. Windows may place the icon inside the hidden `^` tray-icons menu.

The current public build is v1.3.0. The repository currently provides the executable directly in the main branch rather than through a GitHub Release.

The EXE is not digitally signed, so Windows may show a SmartScreen warning. Before running it, confirm that the file came from this repository. If Windows shows the warning, choose **More info** and then **Run anyway** only after verifying the source.

## What you see

| Display | Meaning |
| --- | --- |
| Green keycap with `A` | Caps Lock is on. |
| Gray keycap with `a` | Caps Lock is off. |
| Center overlay | Appears when the state changes, stays above other windows, and disappears automatically. |
| Tray tooltip | Shows `Caps Lock: On` or `Caps Lock: Off`. |
| Right-click menu | Opens settings, language, update checking, startup, support, and exit actions. |

The overlay is shown on the active monitor, does not take focus, and allows mouse clicks to pass through. The default display time is 3 seconds.

## Settings and daily use

- **Overlay settings**: Choose separate colors for on and off states, adjust transparency from 0% to 95%, and set the display duration from 1 to 5 seconds.
- **Language**: Follow the Windows display language, or choose Traditional Chinese or English. The choice is saved for the current Windows user.
- **Start with Windows**: Start automatically after the current user signs in. This uses the current user's startup setting and does not require administrator permission.
- **Refresh status**: Read the current state again when you want an immediate check.
- **Check for updates**: Manually retrieve version information from GitHub. The tool never downloads or installs updates automatically.
- **Support development**: Open the voluntary support options.
- **Exit**: Stop the tray indicator and the overlay.

## How state detection works

1. The tool reads the current Caps Lock state provided by Windows.
2. The notification-area icon is updated to match the state.
3. When the state changes, the overlay appears on the monitor containing the active window.
4. The overlay closes automatically after the configured duration.

The overlay is an event-style notification. It does not appear merely because the program started; press Caps Lock once if you want to verify it.

## Important permissions and limitations

- This is a Windows desktop utility. It does not require PowerShell or the .NET SDK for normal use.
- It does not require administrator permission for normal operation or for **Start with Windows**.
- Preferences and overlay settings are stored under the current user's registry at `HKCU\Software\CapsLockIndicator`.
- The tool does not read documents, keyboard text, Codex data, or other personal files.
- It does not include telemetry or a background reporting service.
- Update checking is the only network-related feature. It retrieves `version.json` from GitHub first and can fall back to the GitHub Release API.
- The executable is currently unsigned, so SmartScreen may display a warning.
- There is no installer or uninstaller. To stop using the tool, exit it and delete the EXE; disable **Start with Windows** first if it was enabled.

## Frequently asked questions

### I cannot find the icon. Where is it?

Check the notification area's hidden icons menu by selecting `^`. If the tool is not there, start `CapsLockIndicator.exe` again.

### The overlay did not appear. Is the tool broken?

The overlay appears when the detected state changes. Press Caps Lock once, then check the center of the active monitor. You can also choose **Refresh status** to update the tray icon.

### The icon and the keyboard state look different.

Choose **Refresh status** from the tray menu. The tool displays the state currently reported by Windows; it does not send a key press or change the state itself.

### How do I stop it from starting with Windows?

Right-click the tray icon and turn off **Start with Windows**. This removes the startup entry for the current Windows user.

### Windows shows a SmartScreen warning. Is that expected?

Yes. The current EXE does not have a digital signature. Verify that it was downloaded from the `Bserz1331/caps-lock-indicator` repository before choosing **More info** and **Run anyway**.

### Why does checking for updates open no download?

The current project keeps version information in `version.json` and currently publishes the EXE directly in the repository rather than in a GitHub Release. If an update is reported, return to this repository and download the current `CapsLockIndicator.exe`.

## Privacy and local data

- The tool only reads the current Caps Lock state exposed by Windows.
- It does not record keyboard text or inspect documents and other personal files.
- It does not upload state, settings, or diagnostics.
- It does not include telemetry or analytics.
- Settings are stored locally in the current user's registry.
- A manual update check contacts GitHub for version information; it does not send keyboard content or personal files.

## Support development

The tray menu's **Support development** action opens voluntary Ko-fi and USDT support options.

You can also visit [ko-fi.com/minz_space_cat](https://ko-fi.com/minz_space_cat). Support does not affect Caps Lock detection or any program feature.

## For developers

<details>
<summary>Source layout and build notes</summary>

### Source layout

- Main source: `src/CapsLockIndicator.cs`
- Compatibility launcher: `scripts/CapsLockIndicator.ps1`
- English launcher: `Start Caps Lock Indicator.bat`
- Traditional Chinese launcher: `啟動 Caps Lock 指示器.bat`
- Remote version manifest: `version.json`
- Icon and preview assets: `assets/`

The repository currently includes a prebuilt Windows executable and the source code, but does not include a one-click build script or project file. End users do not need a compiler, PowerShell, or the .NET SDK to run the published EXE.

The application stores preferences in the current user's registry and uses only the Windows APIs needed to read the Caps Lock state, draw the overlay, and register the optional startup entry.

</details>

## Project status

The current public build is v1.3.0. Issues and compatibility reports should include the Windows version, application version, and minimal reproduction steps. Do not include personal files or keyboard content.
