<p align="center">
  <img src="docs/images/hero.png" alt="Awavio — a colorful eye for a Windows PC that stays awake on your schedule" width="960">
</p>

<h1 align="center">Awavio</h1>
<p align="center"><strong>Stay in your flow.</strong><br>A colorful keep-awake companion for Windows, with a timer you can see.</p>

<p align="center">
  <a href="https://github.com/PbyLoyth/Awavio/releases/latest">Download for Windows</a> ·
  <a href="docs/USAGE.md">User guide</a> ·
  <a href="docs/README.tr.md">Türkçe</a> ·
  <a href="LICENSE">MIT license</a>
</p>

Awavio keeps your PC awake for the time you choose, then lets your normal sleep settings take over again. A small floating eye shows the countdown above your taskbar. Click to start or add time, hold to end, and drag it wherever it fits.

Use it for a long download, a presentation, a render, or work that needs your PC to stay available.

## A little eye, a clear timer

<p align="center">
  <img src="docs/images/duration.png" alt="Awavio duration window with colorful branding, preset durations and days, hours and minutes controls" width="480">
</p>

| Ready when you are | Awake on your schedule |
| :---: | :---: |
| ![Awavio idle widget](docs/images/widget-idle.png) | ![Awavio active countdown widget](docs/images/widget-active.png) |

*Application previews rendered from the native Windows UI. The countdown shown is illustrative.*

## What it does

- **Timed sessions:** choose days, hours and minutes, or use a duration preset.
- **A floating countdown:** start, add time, hold to end, and drag to move.
- **Optional closed-lid operation:** temporarily changes the current power plan's plugged-in and battery lid actions to **Do nothing**.
- **Restores your settings:** puts the previous lid actions back when a session ends. If you select a value other than **Do nothing** during the session, that choice is preserved.
- **Battery cutoff:** ends the session below your selected battery percentage when unplugged; the default is 10%.
- **Display control:** keep the screen on if you need it, or let it turn off while the PC stays awake.
- **Recovery after interruption:** records lid changes before applying them. The installed recovery task retries at sign-in and every five minutes while you are signed in.
- **Optional Windows startup:** turn it on from the menu or during installation.

## Download and install

**Windows 10 or 11, x64.** The release includes its runtime: you do **not** need to install .NET or use administrator rights. ARM64 is not supported by the initial release.

1. Open the [latest release](https://github.com/PbyLoyth/Awavio/releases/latest) and download **Awavio-0.1.0-win-x64.zip**.
2. Extract the whole ZIP.
3. Double-click **Install.cmd**.
4. Open **Awavio** from the Start menu.

The release is not code-signed, so Windows may display a SmartScreen prompt.

For installation with automatic startup, open PowerShell in the extracted folder and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -PublishedDirectory . -StartWithWindows
```

The installer copies the app to `%LOCALAPPDATA%\Programs\Awavio`, adds a Start menu shortcut and registers the **Awavio recovery** task.

### Portable use

Run **Awavio.exe** directly from the extracted folder. Settings are still saved under `%LOCALAPPDATA%\Awavio`. Recovery runs the next time you launch the app; the five-minute recovery task is added only by the installer.

### Your first session

1. Click the eye widget above the taskbar.
2. Choose a duration and select **Start session**.
3. Click again to add time, or hold the widget until the end-session bar fills.

Right-click the widget or tray icon for display, battery, lid and startup settings. If the tray icon is hidden, look under the taskbar's **^** overflow arrow.

Start with a short two- or three-minute session if you plan to use the closed-lid option. Check that the lid setting returns to its previous value afterward. Keep a running laptop on a ventilated surface.

See the [full user guide](docs/USAGE.md) for settings, recovery and troubleshooting.

## Remove or update

To update, extract the new release and run its **Install.cmd**. Your settings are retained.

To remove Awavio, run **Uninstall.cmd** from an extracted release. It restores any unfinished lid change before removing the installation. If restoration fails, it stops without deleting the installation.

To also remove settings and logs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1 -Purge
```

## Build from source

Building requires the **.NET 8 SDK or later** on Windows. Running a release does not.

```powershell
git clone https://github.com/PbyLoyth/Awavio.git
cd Awavio
dotnet test .\tests\Awavio.Core.Tests
dotnet run --project .\src\Awavio.Windows
```

Install a build from the source checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and native power tests.

## How it works

Awavio uses Windows' `SetThreadExecutionState` API to hold a system-awake request. Windows releases that request if the owning thread exits. When the closed-lid option is enabled, Awavio also uses the Windows power-plan APIs and a recovery journal to restore the previous lid actions.

A finished session releases the awake request; it does not force an immediate sleep or turn off the PC. Your Windows power settings determine what happens next.

The app works locally. It requires no account and includes no telemetry or cloud service.

## Validation and scope

This is the first public release. See [VALIDATION.md](VALIDATION.md) for test evidence and the exact environment used.

Physical lid closure, battery discharge and actual sign-out/shutdown still require separate hardware checks. Those conditions are not established by a passing timer or core test. Managed PCs may restrict power-plan changes.

Awavio focuses on Windows timers, lid behavior and recovery. It does not include app freezing, audio muting, Docker rules, hotspot switching or tmux features.

## Project

- [Design notes](docs/DESIGN.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Report a bug](https://github.com/PbyLoyth/Awavio/issues/new)
- [MIT license](LICENSE)
- [Attribution and origins](ATTRIBUTION.md)
