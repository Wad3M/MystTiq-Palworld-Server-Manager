<p align="center">
  <img src="docs/images/github-banner.png" alt="MystTiq Palworld Server Manager Banner">
</p>

<h1 align="center">MystTiq Palworld Server Manager</h1>

<p align="center">
  <strong>The Open-Source Administration Suite for Palworld Dedicated Servers</strong>
</p>

<p align="center">
  <img alt="GitHub release" src="https://img.shields.io/github/v/release/Wad3M/MystTiq-Palworld-Server-Manager?style=for-the-badge">
  <img alt="License" src="https://img.shields.io/github/license/Wad3M/MystTiq-Palworld-Server-Manager?style=for-the-badge">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows_10%20%7C%2011-blue?style=for-the-badge">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-purple?style=for-the-badge">
  <img alt="Open Source" src="https://img.shields.io/badge/Open_Source-MIT-success?style=for-the-badge">
</p>

<p align="center">
  <a href="../../releases">Download Latest Release</a> •
  <a href="docs/">Documentation</a> •
  <a href="../../issues">Report a Bug</a> •
  <a href="../../issues">Request a Feature</a>
</p>

---

## Powerful. Modern. Open Source.

MystTiq Palworld Server Manager is a free and open-source Windows application for hosting, monitoring, maintaining, and troubleshooting **Palworld Dedicated Servers**.

It brings server lifecycle controls, configuration, backups, world inspection, Steam Workshop and UE4SS MOD management, diagnostics, and maintenance workflows into one desktop application.

> **Current development candidate:** v0.2.15.17  
> **Official validated baseline:** v0.2.15.15

Windows is the supported application platform today. The v0.2.15.x architecture work is preparing the backend for a future Linux implementation; Linux support is **not yet released**.

## Highlights

| Area | What MystTiq provides |
|---|---|
| Dashboard | Live lifecycle state, operational health, CPU/RAM history, activity, notifications, and player visibility |
| Server Administration | Start, stop, restart, update, configuration, adoption of an existing PalServer process, and recovery workflows |
| World Management | Player, guild, base, save-data, validation, inspection, and repair-oriented tools |
| Backup & Recovery | One-click backups, restore workflows, validation, history, and maintenance-safe backup handling |
| MOD Platform | Steam Workshop and UE4SS inventory, enable/disable operations, verification, compatibility checks, runtime evidence, and repair recommendations |
| Diagnostics | Server Doctor, crash analysis, runtime/session inspection, and verification-report export |
| Distribution | Portable Windows package, Windows installer, source code, MIT license |

## Dashboard

<p align="center">
  <img src="docs/images/01-dashboard.png" alt="MystTiq dashboard" width="100%">
</p>

## MOD Health and Runtime Evidence

MystTiq deliberately separates **deployment/configuration health** from **runtime proof**.

- **Healthy** means the MOD's required files/state are valid and no confirmed failure is present.
- **Active / Unverified** means the MOD is enabled/deployed but MystTiq does not yet have strong positive runtime evidence. This is not treated as a failure by itself.
- **Confirmed Running / Loaded** is used when positive runtime evidence exists, including supported UE4SS log evidence or native module evidence.
- **Disabled** is intentional and neutral to Overall Health.
- Confirmed missing files, deployment/state conflicts, runtime errors, or other actionable failures can reduce MOD Platform / Overall Health.

Normal modded starts use pre-start reconciliation and a startup health gate. **Start Without MODs** remains an intentional recovery/isolation path.

## Live World Telemetry

v0.2.15.17 adds a **WORLD PULSE** Dashboard strip built from authoritative server/session data rather than guessed timers.

- **World day/time:** read from the decoded active `Level.sav` field `GameTimeSaveData.GameDateTimeTicks`.
- **World save freshness:** based on the active `Level.sav` last-write timestamp.
- **Server session uptime:** based on the current PalServer session start, not MystTiq application uptime.
- **Player pulse:** current online, peak online, joins, leaves, and unique players for the current PalServer session.
- **Backup age:** based on the newest MystTiq backup.
- **Activity feed:** player join/leave and saved-world day transitions can be recorded in Activity.

The saved world clock updates when Palworld writes `Level.sav`. MystTiq does **not** extrapolate between saves or invent a day/night countdown, because sleep-skips and world timing behavior can make uptime-based estimates inaccurate.

## Features

### Server Administration
- Start, stop, restart, and force-stop workflows
- Running-server adoption and session tracking
- CPU and RAM monitoring
- Server update management through SteamCMD
- Lifecycle and operational-health monitoring
- Notification and activity surfaces

### World Management
- World Explorer and validation
- Player Inspector and management tools
- Guild and base inspection
- Save-data inspection and maintenance/repair workflows

### Backup and Recovery
- One-click backups
- Restore workflow
- Backup validation and history
- Maintenance-safe backup handling

### MOD Platform
- Steam Workshop integration
- UE4SS runtime/root resolution
- MOD inventory, install, enable/disable, delete, and state reconciliation
- Runtime evidence from current-session UE4SS signals
- Native UE4SS DLL module evidence where available
- Crossplay/compatibility verification
- Verification report export and repair recommendations

## Screenshot Gallery

### Server Settings
<p align="center"><img src="docs/images/02-settings.png" alt="Server settings" width="100%"></p>

### Player Manager
<p align="center"><img src="docs/images/03-players.png" alt="Player manager" width="100%"></p>

### Guild Manager
<p align="center"><img src="docs/images/04-guilds.png" alt="Guild manager" width="100%"></p>

### World Explorer
<p align="center"><img src="docs/images/05-world-explorer.png" alt="World Explorer" width="100%"></p>

### Backup Manager
<p align="center"><img src="docs/images/06-backups.png" alt="Backup manager" width="100%"></p>

### Mod Management
<p align="center"><img src="docs/images/07-mods.png" alt="Mod management" width="100%"></p>

### Notifications
<p align="center"><img src="docs/images/08-notifications.png" alt="Notifications" width="100%"></p>

## Quick Start

1. Download the latest Windows release.
2. Extract the portable ZIP or run the installer.
3. Launch **MystTiqPalworldServer.exe** and approve the Windows elevation request.
4. Select/configure the Palworld Dedicated Server installation.
5. Start managing the server.

## Requirements

- Windows 10 or Windows 11 (64-bit)
- Palworld Dedicated Server
- Administrator privileges for current Windows management operations
- SteamCMD for server install/update workflows
- UE4SS only when using UE4SS-based MOD functionality

## Download and Installation

The latest builds are available from the GitHub **Releases** page.

Release assets can include:
- Portable Windows ZIP
- Windows installer
- Source code

The installer defaults to `C:\GameServers\MystTiqPalworldServer`, but the installation folder remains user-selectable.

## Building From Source

Clone the repository:

```bash
git clone https://github.com/Wad3M/MystTiq-Palworld-Server-Manager.git
```

Open `PalworldServerManager.slnx`, or use the standard repository workflow:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

The current release build targets Windows x64.

## Architecture and Platform Roadmap

The current application remains WPF/Windows, while backend server lifecycle responsibilities are being separated behind explicit contracts.

Current boundaries include:

```text
MainWindow
  -> ApplicationServiceComposition
     -> ServerService
        -> IServerSessionInspector
           -> ServerSessionInspector (Windows)
        -> IServerPlatformOperations
           -> WindowsServerPlatformOperations
        -> ServerLifecycleEvaluator
```

v0.2.15.16 additionally centralizes server process/executable conventions in `ServerPlatformProfile`, removing Windows PalServer process names from `ServerService`.

| Version | Status | Focus |
|---|:---:|---|
| **v0.2.15.15** | Official baseline | Server platform operations abstraction |
| **v0.2.15.16** | Predecessor RC | Platform profile abstraction plus README/documentation consistency cleanup |
| **v0.2.15.17** | Current RC | Live World Telemetry & Dashboard Pulse using save-backed world clock and session metrics |
| **v0.2.15.x** | In progress | Complete remaining Windows/backend abstraction seams while preserving validated behavior |
| **v0.3.0.0** | Planned | Begin Linux support implementation on the platform boundaries established in v0.2.15.x |
| **v1.0** | Goal | Stable production release |

Detailed historical changes belong in [CHANGELOG.md](CHANGELOG.md) and the `release-notes` folder rather than being duplicated in this README.

## Documentation

Repository documentation includes:
- [CHANGELOG.md](CHANGELOG.md) — release history
- [CONTRIBUTING.md](CONTRIBUTING.md) — contribution workflow
- [SECURITY.md](SECURITY.md) — security reporting
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) — community standards
- `release-notes/` — version-specific release/build/test notes
- `docs/` — public documentation and screenshots

## Contributing

Contributions, bug reports, feature requests, and documentation improvements are welcome. Please review [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) before submitting changes.

## Security

If you discover a security issue or accidentally expose credentials, follow [SECURITY.md](SECURITY.md).

## License

Released under the **MIT License**. See [LICENSE](LICENSE) for complete details.

## Disclaimer

MystTiq Palworld Server Manager is an independent community project. It is **not affiliated with, endorsed by, or sponsored by Pocketpair, Inc.** Palworld and related trademarks are the property of their respective owners.

## About MystTiq

MystTiq is an open-source initiative focused on approachable management tools for self-hosted game servers. The long-term goal is a consistent management experience across supported dedicated-server platforms while remaining free and open source.

---

<p align="center"><strong>If MystTiq has been helpful, consider starring the repository.</strong></p>
<p align="center">Built for the self-hosting community.</p>
