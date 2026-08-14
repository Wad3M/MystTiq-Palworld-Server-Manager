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

## Overview

MystTiq Palworld Server Manager is a free and open-source Windows desktop application for hosting, monitoring, maintaining, and troubleshooting **Palworld Dedicated Servers**.

It combines server lifecycle controls, configuration, backups, world inspection, SteamCMD management, Steam Workshop and UE4SS MOD workflows, diagnostics, health reporting, and live world telemetry in one interface.

> **Current development candidate:** v0.3.0.7  
> **Official validated Windows baseline:** v0.2.16.4
> **Official Linux/headless baseline:** v0.3.0.5

Windows 10/11 x64 remains the supported GUI application platform. v0.3 begins the Linux/headless implementation on top of the frozen v0.2.16.4 Windows baseline. **Linux support remains experimental throughout the v0.3 implementation/parity line and is not yet a production release.**

## Highlights

| Area | What MystTiq provides |
|---|---|
| Dashboard | Server state, operational health, CPU/RAM history, activity, notifications, players, and WORLD PULSE telemetry |
| Server Administration | Start, stop, restart, force stop, update, configuration, adoption, and recovery workflows |
| World Management | World, player, guild, base, save-data, validation, inspection, and repair-oriented tools |
| Backup & Recovery | One-click backups, restore workflows, validation, history, and maintenance-safe handling |
| MOD Platform | Steam Workshop and UE4SS inventory, enable/disable, verification, compatibility checks, runtime evidence, and repair recommendations |
| Diagnostics | Server Doctor, crash analysis, runtime/session inspection, verification-report export, and environment checks |
| Distribution | Portable Windows package, Windows installer, source code, checksums, and built-in MystTiq release awareness |

## Dashboard

<p align="center">
  <img src="docs/images/01-dashboard.png" alt="MystTiq dashboard" width="100%">
</p>

The Dashboard is designed to answer two questions quickly:

1. **Is the server operational?**
2. **What is happening in the world right now?**

Operational Health is intentionally separate from informational/runtime-confidence states so uncertainty does not incorrectly make a healthy server appear degraded.

### WORLD PULSE

WORLD PULSE combines authoritative saved-world data with the active PalServer session:

- saved Palworld day/time
- save freshness
- current PalServer session uptime
- current and peak online players
- session joins, leaves, and unique players
- latest backup age
- most recent player transition

World time is read from saved-world evidence and is not extrapolated from process uptime.

## MOD Health and Runtime Evidence

MystTiq separates **deployment/configuration health** from **runtime proof**.

- **Healthy** — required files/state are valid and no confirmed failure is present.
- **Confirmed Running / Loaded** — positive runtime evidence exists.
- **Active / Unverified** — enabled/deployed, but no strong runtime proof is currently available. This is neutral to Overall Health.
- **Disabled** — intentionally disabled and neutral to Overall Health.
- **Failed / Error** — a confirmed actionable failure that can reduce MOD Platform and Overall Health.

Runtime evidence can include supported UE4SS log signals and native DLL/module evidence. Normal modded starts use pre-start reconciliation and a startup health gate. **Start Without MODs** remains an intentional recovery/isolation path.






## Secure Remote API Enrollment & TLS Provisioning

v0.3.0.7 turns the existing authentication/TLS boundary into an explicit, testable LAN enrollment workflow while preserving loopback-only operation as the default.

New headless commands:

```bash
./mysttiq-server api-tls-create ...
./mysttiq-server api-remote-enable ...
./mysttiq-server api-remote-disable ...
```

The self-signed certificate generator creates a 3072-bit RSA server certificate with:

- SHA-256 signature
- TLS Web Server Authentication EKU
- IP SAN for the selected bind address
- `localhost` SAN
- optional DNS SAN
- validity limited to 825 days
- password stored in a separate protected secret file

Remote API exposure remains explicit. `api-remote-enable` requires a non-loopback literal IP and writes a configuration where both bearer authentication and TLS are enabled. `api-remote-disable` returns the API to the safe `127.0.0.1:8213` default.

One-command Linux enrollment:

```bash
bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

The script prompts for confirmation, performs one `sudo` authorization, creates/reuses the token and certificate, fixes service-user ownership/permissions, updates the configuration, reinstalls/restarts the service, validates systemd, and tests authenticated HTTPS locally.

Windows LAN acceptance:

```powershell
.\scripts\Test-MystTiqRemoteApi.ps1
```

The Windows test retrieves the bearer token through the existing trusted SSH-key channel into process memory only and verifies:

- HTTPS reachability from Windows
- `/healthz` returns HTTP 200
- unauthenticated management access returns HTTP 401
- valid bearer authentication returns HTTP 200
- lifecycle JSON is returned

MystTiq does **not** automatically change Linux firewall rules during enrollment.

## Passwordless Linux Deployment & SSH Trust

v0.3.0.5 makes a dedicated SSH key the normal MystTiq deployment path.

One-time setup from Windows PowerShell:

```powershell
.\scripts\Initialize-MystTiqLinuxSSH.ps1
```

The helper creates a dedicated Ed25519 key under the current user's `.ssh` directory when needed, installs **only the public key** into the Linux account's `authorized_keys`, and verifies passwordless login.

Normal deployment then becomes:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

The deployment wrapper:

- prefers the dedicated MystTiq SSH identity automatically
- uses OpenSSH `BatchMode=yes` with password authentication disabled while key authentication is active
- uses the same identity for `ssh` and `scp`
- fails with a clear setup instruction if the dedicated key is missing or invalid
- allows interactive password prompting only when `-AllowPasswordFallback` is explicitly requested
- never copies the private SSH key to Linux
- continues to verify the transferred archive by SHA-256 before extraction
- invokes the version-matched Linux acceptance runner automatically

The default test target remains `mystroth@192.168.1.248`.

## Headless Configuration & Local Management API

v0.3.0.3 introduces persistent Linux headless configuration and the first management API boundary.

Default configuration:

```text
/etc/mysttiq/mysttiq.json
```

Example configuration:

```text
config/mysttiq.linux.example.json
```

Configuration commands:

```bash
./mysttiq-server config-show
./mysttiq-server config-validate
sudo ./mysttiq-server config-write-default
```

The configuration currently owns:

- PalServer / SteamCMD / backup / runtime paths
- PalServer launch arguments
- startup and graceful-stop timeouts
- service polling
- automatic-recovery backoff, budget, and window
- local API enablement, bind address, and port

The default local management endpoint is:

```text
http://127.0.0.1:8213
```

v0.3.0.3 deliberately rejects non-loopback API bindings. Remote/LAN exposure and authentication are not enabled in this phase.

Local API endpoints:

```text
GET  /healthz
GET  /api/v1/status
GET  /api/v1/service
GET  /api/v1/config
POST /api/v1/server/start
POST /api/v1/server/stop
POST /api/v1/server/restart
```

The service host starts the API alongside the existing `LinuxHeadlessSupervisor`. Lifecycle POST operations are serialized so simultaneous start/stop/restart requests cannot race each other.

## Linux systemd Service & Automatic Recovery

v0.3.0.2 moves the Linux headless host from an SSH-invoked tool toward a real background service.

The new service commands are:

```bash
./mysttiq-server service-status
sudo ./mysttiq-server service-install
sudo ./mysttiq-server service-install --start-now
sudo ./mysttiq-server service-uninstall
```

`service-install` copies the current self-contained host to `/opt/mysttiq/bin/mysttiq-server`, creates `mysttiq-palworld.service`, reloads systemd, and enables boot startup. Starting the service remains explicit unless `--start-now` is supplied.

The installed service runs the long-lived `service-run` supervisor. MystTiq—not systemd directly—continues to own PalServer lifecycle policy. The supervisor:

- starts or adopts the native Linux PalServer
- polls observed lifecycle/process/port state
- detects unexpected PalServer disappearance
- attempts bounded automatic recovery with backoff
- exits on repeated recovery failure so systemd's own `Restart=on-failure` policy can take over
- catches SIGTERM/SIGINT and requests graceful PalServer shutdown before the service exits
- logs service-level output through the systemd journal while preserving PalServer's detached console log

The systemd unit uses `Restart=on-failure`, a 10-second restart delay, bounded systemd start attempts, `NoNewPrivileges=true`, and a dedicated non-root service user selected at installation time.

## Linux Headless Lifecycle Control

v0.3.0.1 grants the experimental Linux headless host its first controlled lifecycle authority.

```bash
./mysttiq-server status
./mysttiq-server start
./mysttiq-server stop
./mysttiq-server restart
```

The lifecycle layer:

- blocks duplicate starts
- launches `PalServer.sh` detached from the interactive SSH session
- observes the real `PalServer-Linux-Shipping` process
- verifies UDP `8211` before declaring startup ready
- records lifecycle state under `/opt/mysttiq/runtime`
- sends **SIGTERM first** for graceful shutdown
- escalates to **SIGKILL only after the configured graceful timeout**
- detects disappearance of a previously observed/managed process as a possible crash
- writes detached console output to `/opt/mysttiq/runtime/palserver-console.log`
- returns stable headless exit codes suitable for future service/automation integration

The Windows WPF application is not migrated to this lifecycle host in v0.3.0.1. Windows headless/service and low-resource UI adoption remains scheduled for the v0.4 line.

## Headless Core & Linux Foundation

v0.3.0.0 introduces two new cross-platform projects without replacing the proven Windows WPF application:

```text
MystTiq.Core
    └── platform-neutral models, profiles, Linux distribution detection,
        SteamCMD distribution policy, and Linux read-only session inspection

MystTiq.HeadlessHost
    └── no-WPF command-line host for probe/status/install-plan operations
```

The initial headless host is intentionally **non-destructive**. It can detect the Linux distribution, resolve Linux PalServer/SteamCMD paths, discover native PalServer processes, inspect guarded ports, and display the Linux SteamCMD install/update plan without starting, stopping, installing, or updating anything. Lifecycle authority is reserved for a later v0.3 phase.

The Linux SteamCMD policy includes:

```text
+@sSteamCmdForcePlatformType linux
```

This is required by the validated reference environment; without the explicit platform override SteamCMD returned `Missing configuration` for App `2394010` even though the Linux depot was available.

### Linux reference environment

The v0.3.0.0 Linux foundation was validated against:

- **Ubuntu Server 24.04.4 LTS (Noble Numbat)**
- **x86_64 / amd64**
- **Linux kernel 6.8.0-137-generic**
- Valve Linux SteamCMD
- Palworld Dedicated Server Steam App `2394010`
- native Linux server executable `Pal/Binaries/Linux/PalServer-Linux-Shipping`
- validated game listener: UDP `8211`

Detailed environment notes are maintained in [`docs/linux/TESTED_ENVIRONMENT.md`](docs/linux/TESTED_ENVIRONMENT.md).

## Features

### Server Administration

- Start, stop, restart, and force-stop workflows
- Running-server adoption and active-session tracking
- CPU/RAM monitoring
- SteamCMD server install, update, and validation
- Server configuration and environment verification
- Lifecycle and operational-health monitoring
- Activity and notification surfaces

### World Management

- World Explorer and World Inspector
- Live-safe `Level.sav` snapshot reading
- Player Inspector and management tools
- Guild and base inspection
- Save-data validation
- Maintenance/repair workflows

### Backup and Recovery

- One-click backups
- Restore workflow
- Backup validation/history
- Maintenance-safe backup handling

### MOD Platform

- Steam Workshop integration
- UE4SS runtime/root resolution
- MOD inventory, install, enable/disable, delete, and reconciliation
- Current-session UE4SS runtime evidence
- Native UE4SS DLL module evidence
- Crossplay/compatibility verification
- Verification report export
- Repair recommendations

### Application Update Awareness

Server Setup and Update Center can compare the installed MystTiq version with the latest published full GitHub release.

MystTiq reports:

- **UPDATE AVAILABLE**
- **UP TO DATE**
- **DEVELOPMENT BUILD**
- **CHECK FAILED**

The check is informational; MystTiq does not silently replace the running application.

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

### MOD Management
<p align="center"><img src="docs/images/07-mods.png" alt="MOD management" width="100%"></p>

### Notifications
<p align="center"><img src="docs/images/08-notifications.png" alt="Notifications" width="100%"></p>

## Quick Start

1. Download the latest Windows release.
2. Extract the portable ZIP or run the installer.
3. Launch `MystTiqPalworldServer.exe` and approve elevation when required.
4. Select or configure the Palworld Dedicated Server installation.
5. Verify the environment.
6. Start managing the server.

## Requirements

- Windows 10 or Windows 11, 64-bit
- Palworld Dedicated Server
- Administrator privileges for current Windows management operations
- SteamCMD for server install/update workflows
- UE4SS only when using UE4SS-based MOD functionality

## Installation

Release assets are published on the GitHub **Releases** page.

The Windows installer defaults to:

```text
C:\GameServers\MystTiqPalworldServer
```

The installation directory remains user-selectable.

## Building From Source

Clone the repository:

```bash
git clone https://github.com/Wad3M/MystTiq-Palworld-Server-Manager.git
```

Then use the standard repository workflow:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

The standard release build continues to target Windows x64. The experimental Linux headless host can be cross-published separately with:

```powershell
.\Build.ps1 LinuxHeadless
```

## Architecture and Platform Direction

The supported GUI application remains WPF/Windows. v0.3 adds a separate cross-platform core and headless host so Linux can run without any GUI dependency while Windows behavior remains protected by the v0.2.16.4 baseline.

The repository now has two architectural tracks:

```text
Windows GUI (validated baseline)
PalworldManager / WPF
  ├── existing v0.2.16.4 platform boundaries
  └── preserved as the Windows regression reference

Headless / cross-platform foundation
MystTiq.HeadlessHost
  └── MystTiq.Core
       ├── ServerPlatformProfile
       ├── IServerPathProfile
       │    ├── WindowsServerPathProfile
       │    └── LinuxServerPathProfile
       ├── IServerDistributionPlatformService
       │    ├── WindowsServerDistributionPlatformService
       │    └── LinuxServerDistributionPlatformService
       ├── LinuxDistributionService
       └── LinuxServerSessionInspector
```

The v0.3 core begins with Linux observation, paths, and distribution policy. Proven Windows services will migrate into the shared core incrementally only when the move can remain behavior-neutral.

The remaining Windows-specific work is primarily the desktop UI/host layer, file/folder integration, and Windows-only dependency tooling. Detailed platform-audit information is maintained in [`docs/architecture/`](docs/architecture/).

### Roadmap

| Version | Status | Focus |
|---|:---:|---|
| **v0.2.16.4** | Official Windows baseline | Final Windows platform-preparation baseline |
| **v0.3.0.7** | Current RC | Linux Integration & Production Readiness |
| **v0.3.x** | Planned | Linux lifecycle, SteamCMD execution, systemd, API/configuration, parity and hardening |
| **v0.4.x** | Planned | Windows service/headless improvements, character/account migration, Server Doctor consolidation, setup/update cleanup |
| **v0.5.x** | Planned | Advanced administration, automation, secure remote management, analytics, backups, guild/player tools |
| **v0.6.x** | Planned | Multi-server / multi-instance Palworld management |
| **v0.7.x** | Planned | Themes, skins, UI personalization and original Palworld-inspired MystTiq icon set |
| **v0.8.x** | Planned | Adaptive application/server/network efficiency, priorities, eco modes and host/per-server dashboards |
| **v1.0** | Goal | Stable production release |

Historical release implementation detail belongs in [`CHANGELOG.md`](CHANGELOG.md), [`release-notes/`](release-notes/), and [`docs/history/`](docs/history/) rather than being duplicated here.

## Documentation

The documentation layout is intentionally separated by purpose:

- [`README.md`](README.md) — current product overview
- [`CHANGELOG.md`](CHANGELOG.md) — complete release history
- [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) — active release/promotion checklist
- [`docs/README.md`](docs/README.md) — documentation index
- [`docs/architecture/`](docs/architecture/) — current architecture/audit documents
- [`docs/linux/`](docs/linux/) — Linux reference environment and headless implementation notes
- [`docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md`](docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md) — improvements discovered during Linux work for shared/core or v0.4 Windows backport
- [`docs/roadmap/PRODUCT_ROADMAP.md`](docs/roadmap/PRODUCT_ROADMAP.md) — detailed roadmap through v0.8
- [`docs/history/`](docs/history/) — archived architecture and feature implementation notes
- [`docs/release/`](docs/release/) — publication/release process documents
- [`release-notes/`](release-notes/) — version-specific build, test, apply, and release notes
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution workflow
- [`SECURITY.md`](SECURITY.md) — security reporting
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) — community standards

## Contributing

Contributions, bug reports, feature requests, and documentation improvements are welcome. Please review [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Security

For security issues or accidentally exposed credentials, follow [SECURITY.md](SECURITY.md).

## License

Released under the **MIT License**. See [LICENSE](LICENSE).

## Disclaimer

MystTiq Palworld Server Manager is an independent community project. It is **not affiliated with, endorsed by, or sponsored by Pocketpair, Inc.** Palworld and related trademarks are the property of their respective owners.

---

<p align="center"><strong>If MystTiq has been helpful, consider starring the repository.</strong></p>
<p align="center">Built for the self-hosting community.</p>
