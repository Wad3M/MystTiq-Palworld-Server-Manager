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
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-blue?style=for-the-badge">
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

MystTiq Palworld Server Manager is a free and open-source administration suite for hosting, monitoring, maintaining, and troubleshooting **Palworld Dedicated Servers** — on Windows or Linux.

It combines server lifecycle controls, configuration, backups, world inspection, SteamCMD management, Steam Workshop and UE4SS MOD workflows, diagnostics, health reporting, scheduled automation, and live world telemetry in one interface.

> **Current release candidate:** v0.8.25.0 — Every Colour Follows the Mode
> **Accepted baseline:** v0.8.25.0 — Every Colour Follows the Mode (set 2026-09-25; checkpoint `_Backups\MystTiqPalworldServer\v0.8.25.0`; previous: v0.8.1.0)
> **Official validated Windows baseline:** v0.2.16.4
> **Official Linux/headless baseline:** v0.3.0.5

The management layer is a persistent, headless-first background service (`MystTiq.Core` + `MystTiq.HeadlessHost`) that owns every PalServer lifecycle, configuration, backup, world-mutation, and automation decision. A shared Avalonia desktop client (`MystTiq.Desktop`) runs on both Windows and Linux as a thin API client — closing or crashing the GUI never stops the managed server. See [Architecture and Platform Direction](#architecture-and-platform-direction) below.

For what shipped in the current release candidate, see [`CHANGELOG.md`](CHANGELOG.md) and [`release-notes/`](release-notes/) — implementation detail belongs there, not duplicated in this file.

## Highlights

| Area | What MystTiq provides |
|---|---|
| Dashboard | Server state, operational health, CPU/RAM history, activity, notifications, players, and WORLD PULSE telemetry |
| Server Administration | Start, stop, restart, force stop, update, configuration, adoption, and recovery workflows |
| World Management | World, player, guild, base, save-data, validation, inspection, and repair-oriented tools |
| Backup & Recovery | One-click backups, restore workflows, formal backup classes with retention protection, validation, history, and maintenance-safe handling |
| Automation & Alerts | Scheduled backup/lifecycle automation, RCON warning countdowns, threshold and disk-space-prediction alerting |
| Security | Role-based API access (Viewer/Operator/Admin/Owner) layered on top of authenticated bearer/TLS remote management |
| Multi-Server Fleet | Any number of independently isolated servers under one management process, fleet dashboard, staggered Backup/Doctor/Update All, per-server role scoping |
| Windows Service | Install, start, stop and monitor MystTiq as a real Windows Service with automatic crash recovery, matching the existing Linux systemd support |
| Character Migration | Migrate a player's guild membership to a new account identity (e.g. Xbox → Steam) with preview, safety backup, and audited source-character disposition |
| Diagnostics Platform | One unified Doctor/Environment health report driving the real Overall Health badge, Fix Automatically, per-check Recheck, and client-side DNS/TCP/TLS/HTTP connection diagnostics that work even when the server is unreachable |
| Provider Framework | Kick/ban routes through REST first, automatically falling back to Palworld RCON when REST is disabled or unreachable, with visible per-provider health; cross-setting port-conflict detection surfaces as a real Diagnostics finding |
| Player Registry | Persistent per-player first/last seen, session count, tracked playtime, and Steam ID/UID mapping, plus a bounded join/leave event history and abandoned-base detection from orphaned guild data |
| World Editing & Recovery | Guild membership repair (remove a dangling member reference with no matching save) on the proven transactional pattern; player identity mismatch detection (Steam ID collisions, missing save files) as a real Diagnostics finding |
| MOD Safety & Alerts | Automatic pre-mutation snapshot and one-click rollback for every MOD install/delete; Alert Center notification when an enabled MOD's health degrades |
| Idle Auto-Stop | Warns, does a final live player recheck, then stops a server that's had 0 players for a configurable number of minutes — reuses the same RCON broadcast/lifecycle-stop path as any other automation rule |
| Clone World | Duplicates a server profile's entire installation (binaries + world) into a new, independent profile with non-colliding ports, for running a second copy alongside the original |
| MOD Platform | Steam Workshop and UE4SS inventory, enable/disable, verification, compatibility checks, runtime evidence, and repair recommendations |
| Diagnostics | Server Doctor, crash analysis, runtime/session inspection, verification-report export, and environment checks |
| Host & Performance | A HOST tab for the server's machine: processor, memory, disks, network and a 7-day history; per-server priority, eco mode (Windows efficiency mode, or automatically when nobody is online), processor cores and bandwidth limits |
| Live World Map | Players (online and last known), guild bases and working Pals on the world map, with zoom, clustering, click-to-teleport and names from the game's own files |
| Players & Community | Give items or Pals from a picker of the game's own ids, starter kits, teleport points players reach by chat command, whitelist, temporary bans and a Discord bot with live status |
| Accounts & Roles | Named user accounts (salted passwords, sessions, lockout) and every screen following the signed-in role: cards, page buttons and the Ribbon are hidden or disabled where the server would refuse |
| Crash Analysis | Reads Unreal crash reports and server logs, matches real signatures with causes and fixes, and alerts on a new critical crash |
| Network Checks | Router WAN address, double-NAT and CGNAT detection, and UPnP port mapping that works on Linux and multi-interface Windows |
| Nexus Mods | Browse and install mods from Nexus Mods inside the app (Premium direct downloads, nxm links for free accounts) |
| Appearance & Language | Dark, Light, Midnight (true black), High contrast (including Windows contrast themes) and Follow the system, Compact or Comfortable density, and English, German and Spanish |
| Distribution | Portable Windows package, Windows installer, Linux headless service, source code, checksums, and built-in MystTiq release awareness |

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

## Scheduled Automation, Alerts & Backup Classes

A persisted trigger → condition → action engine schedules backups, lifecycle actions, notifications, and RCON commands, including warning-countdown broadcasts before a scheduled restart. Backups carry a class (Manual/Scheduled/Emergency/Safety); routine retention cleanup only ever touches Scheduled backups by default, so manual and safety copies are protected. An Alert Center watches CPU, memory, and disk space — including a real disk-space-exhaustion projection — and routes into the same Notification Center used everywhere else in the app.

## Role-Based API Access

A minimal role model (Viewer / Operator / Admin / Owner) sits on top of the existing authenticated bearer-token/TLS boundary. Every existing zero-config deployment keeps working unchanged — the original shared bearer token still resolves to full administrative access — while an Owner can issue additional, narrower-scoped tokens (including time-limited guest access) for other operators without sharing the primary credential.

## Multi-Server Fleet

One management process can now run any number of independently isolated Palworld servers — each with its own paths, backups, automation rules, and lifecycle state — while every existing single-server deployment keeps working with zero configuration changes (an existing config upgrades automatically to a single-entry fleet). The Fleet page lists every configured server with independent live status, and Backup All / Doctor All / Update All run staggered, fleet-wide actions from one place. A role can be scoped to just one server in the fleet, and a lock on one server's world/lifecycle state never blocks another's.

## Secure Remote API Enrollment & TLS Provisioning

Remote/LAN management is an explicit, testable enrollment workflow. Loopback-only operation remains the default.

Headless commands:

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

A dedicated SSH key is the standard MystTiq deployment path.

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

## Headless Configuration & Local Management API

Persistent Linux headless configuration and the management API boundary.

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

The configuration owns:

- PalServer / SteamCMD / backup / runtime paths
- PalServer launch arguments
- startup and graceful-stop timeouts
- service polling
- automatic-recovery backoff, budget, and window
- local API enablement, bind address, and port
- authentication/RBAC and TLS boundaries

The default local management endpoint is:

```text
http://127.0.0.1:8213
```

The API rejects non-loopback bindings unless authentication and TLS are both explicitly enabled (see [Secure Remote API Enrollment](#secure-remote-api-enrollment--tls-provisioning) above).

Core local API endpoints:

```text
GET  /healthz
GET  /api/v1/status
GET  /api/v1/service
GET  /api/v1/config
POST /api/v1/server/start
POST /api/v1/server/stop
POST /api/v1/server/restart
```

Lifecycle mutations are serialized through a central Operation Coordinator, so simultaneous start/stop/restart requests — and any concurrently in-flight world/guild/base save mutation — cannot race each other.

## Linux systemd Service & Automatic Recovery

The Linux headless host runs as a real background service.

Service commands:

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
- returns stable headless exit codes suitable for service/automation integration

## Headless Core & Cross-Platform Foundation

`MystTiq.Core` and `MystTiq.HeadlessHost` are the shared, no-GUI foundation both platforms run on:

```text
MystTiq.Core
    └── platform-neutral models, profiles, distribution detection,
        SteamCMD policy, session inspection, operation/automation/
        security contracts, and per-platform lifecycle services

MystTiq.HeadlessHost
    └── the management API host: lifecycle, configuration, backups,
        world/guild/base transactions, automation scheduler, alerts,
        RBAC, and every other server-authoritative feature
```

The headless host can detect the OS, resolve PalServer/SteamCMD paths, discover native PalServer processes, inspect guarded ports, and drive the full SteamCMD install/update plan on either platform.

The Linux SteamCMD policy includes:

```text
+@sSteamCmdForcePlatformType linux
```

This is required by the validated reference environment; without the explicit platform override SteamCMD returned `Missing configuration` for App `2394010` even though the Linux depot was available.

### Linux reference environment

The Linux foundation is validated against:

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
- Guild and base inspection, ownership transfer, and recovery
- Save-data validation
- Maintenance/repair workflows, all transactional (preview → safety backup → server-side transaction → validate → journal/audit)

### Backup and Recovery

- One-click backups with Manual/Scheduled/Emergency/Safety classification
- Restore workflow
- Backup validation/history
- Class-scoped retention preview/apply
- Maintenance-safe backup handling

### Automation & Analytics

- Scheduled (daily-time or interval) trigger → condition → action rules
- Backup, lifecycle, notification, and RCON actions with warning-countdown broadcasts
- Alert Center: sustained CPU/memory thresholds, low disk space, disk-space-exhaustion prediction
- Historical CPU/RAM/world-size charts with honest gap and restart markers

### MOD Platform

- Steam Workshop integration
- UE4SS runtime/root resolution
- MOD inventory, install, enable/disable, delete, and reconciliation
- Current-session UE4SS runtime evidence
- Native UE4SS DLL module evidence
- Crossplay/compatibility verification
- Verification report export
- Repair recommendations

### Host & Performance

- The machine's processor, memory, uptime, disks (which hold the install, saves and backups) and network adapters
- A history of processor, memory and upload over an hour, a day or a week (kept 7 days)
- Per-server process priority, eco mode (always, or after the server has been empty for a while) and processor cores,
  applied as soon as the server starts and checked every 15 seconds
- Per-server bandwidth limits written into the server's `Engine.ini` before every start, with the game's real defaults
  and an upload estimate

### Players & Community

- Live world map: players, last-known positions, guild bases and working Pals
- Give items and Pals, starter kits with auto-gift, teleport points by chat command
- Whitelist, temporary bans, player notes and warnings
- Discord bot (status message, presence, events feed, commands) and outside delivery by Discord, email and webhooks,
  which can be paused per server

### Accounts, Roles and Remote Management

- Named user accounts with roles Viewer, Operator, Admin and Owner, optionally limited to one server
- Every route declares the role it needs; the Desktop disables or hides what the signed-in role cannot use
- Remote management over pinned TLS, tested from the Desktop on Windows and on Linux

### Appearance & Language

- Dark, Light, Midnight, High contrast (Windows contrast themes included) and Follow the system, per tab
- Compact or Comfortable density
- English, German and Spanish (finishing the translation is v0.9)
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

### World Validator
<p align="center"><img src="docs/images/09-validator.png" alt="World Validator" width="100%"></p>

### Automation
<p align="center"><img src="docs/images/10-automation.png" alt="Automation" width="100%"></p>

### Alert Center
<p align="center"><img src="docs/images/11-alert-center.png" alt="Alert Center" width="100%"></p>

### Security
<p align="center"><img src="docs/images/12-security.png" alt="Security" width="100%"></p>

## Quick Start

1. Download the latest release for your platform.
2. **Windows:** extract the portable ZIP or run the installer, then launch `MystTiqPalworldServer.exe`.
   **Linux:** extract the headless tarball and run `./mysttiq-server service-install --start-now` (see [Linux systemd Service](#linux-systemd-service--automatic-recovery) above), or run the Avalonia desktop build directly.
3. Select or configure the Palworld Dedicated Server installation.
4. Verify the environment.
5. Start managing the server.

## Requirements

- Windows 10/11 (64-bit) **or** Linux (Ubuntu 24.04 LTS is the validated reference environment; other modern distributions are expected to work)
- Palworld Dedicated Server
- SteamCMD for server install/update workflows
- UE4SS only when using UE4SS-based MOD functionality
- Administrator/root privileges for install-time and service-management operations

## Installation

Release assets are published on the GitHub **Releases** page.

The Windows installer defaults to:

```text
C:\GameServers\MystTiqPalworldServer
```

The installation directory remains user-selectable. On Linux, `service-install` installs the headless host to `/opt/mysttiq/bin/mysttiq-server`.

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
```

Run the current release candidate's version-aware logic gate, which resolves the version from `Directory.Build.props`, unblocks scripts, runs Clean, runs strict Validate, then runs that version's logic tests with its own build gate:

```powershell
.\scripts\Test-CurrentRelease.ps1 -ProjectRoot . -ExportJson
```

Cross-publish individual targets:

```powershell
.\Build.ps1 DesktopWindows   # Windows Avalonia desktop + headless sidecar
.\Build.ps1 DesktopLinux     # Linux Avalonia desktop + headless sidecar
.\Build.ps1 WindowsHeadless  # Windows persistent headless host
.\Build.ps1 LinuxHeadless    # Linux headless host + systemd deployment scripts
```

## Architecture and Platform Direction

MystTiq is headless-first: a persistent management service owns every server-authoritative decision, and every GUI is a thin client of it.

```text
MystTiq.Desktop (Avalonia, Windows + Linux)
        ↓ authenticated management API
MystTiq.HeadlessHost (persistent service / systemd unit)
        ↓
MystTiq.Core (shared platform-neutral contracts and services)
        ↓
PalServer
```

```text
MystTiq.Core
  ├── ServerPlatformProfile / IServerPathProfile (Windows + Linux)
  ├── IServerDistributionPlatformService (Windows + Linux)
  ├── IServerLifecycleService (Windows + Linux)
  ├── Operations/   -- OperationCoordinator, resource-key locking, operation records
  ├── Automation/   -- scheduled trigger/condition/action contracts
  └── Security/     -- role-based principal contracts

MystTiq.HeadlessHost
  └── the management API: lifecycle, configuration, backups, world/guild/base
      transactions, automation scheduler, alerts, RBAC, MOD management, and
      every other server-authoritative feature

MystTiq.Desktop
  └── Avalonia MVVM shell; no direct PalServer or remote-filesystem ownership

src/PalworldManager
  └── the original Windows-only WPF client, kept as the historical
      feature-parity reference during the Avalonia migration
```

Closing or crashing any GUI never stops the managed server or the persistent service. Detailed platform-audit information is maintained in [`docs/architecture/`](docs/architecture/).

### Roadmap

| Version | Status | Focus |
|---|:---:|---|
| **v0.2.16.4** | Shipped | Final Windows-only platform baseline |
| **v0.3.x** | Shipped | Linux/headless foundation: lifecycle, SteamCMD, systemd, management API, secure enrollment |
| **v0.4.x** | Shipped | Windows persistent service foundation, network diagnostics, Avalonia navigation/theme foundation |
| **v0.5.x** | Shipped | Functional Visual Shell Integration: full Avalonia GUI parity with the legacy WPF app, guild/base ownership transaction platform, desktop shell UI/UX consolidation |
| **v0.6.0.0** | Shipped | Architecture baseline: Operation Coordinator, resource-key locking |
| **v0.6.1.0** | Shipped | Advanced administration, automation, RBAC, analytics, formal backup classes |
| **v0.6.2.0** | Shipped | Multi-server fleet: plural server profiles, per-server isolation and locking, fleet dashboard and bulk actions |
| **v0.6.3.0** | Shipped | Windows service hardening, character/account migration, Server Setup vs. Update Center cleanup |
| **v0.6.4.0** | Shipped | Unified Doctor/Environment diagnostics, real Overall Health, Fix Automatically, local-PC connectivity diagnostics |
| **v0.6.5.0** | Shipped | Provider Framework (REST/RCON player-moderation fallback) and Configuration Intelligence's first slice (port-conflict detection) |
| **v0.6.6.0** | Shipped | Persistent Player Registry (identity/session history, Steam ID/UID mapping, join/leave events) and abandoned-base detection |
| **v0.6.7.0** | Shipped | Guild membership repair (Remove Broken Member) and player identity mismatch detection |
| **v0.6.8.0** | Shipped | MOD backup/staged install/rollback and Alert Center integration for MOD/UE4SS health |
| **v0.6.9.0** | Shipped | Idle Auto-Stop with warning and final player recheck |
| **v0.6.10.0** | Shipped | Clone World feature, plus extended live verification (sustained run, real 2-instance fleet, cross-machine connection, closed the v0.6.9.0 idle-stop verification gap) |
| **v0.6.11.0** | Shipped | Fixes a real stale-backend bootstrap bug (version-blind instance reuse), UI polish (Start button, nav color, header tagline), and new WAN reachability diagnostics (public IP, router UPnP port-mapping detection/repair, external port-checker hand-off) |
| **v0.6.12.0** | Shipped | Full project gap audit against every prior checkpoint plus live bug/logic testing; fixes a real ~9-month-old explorer-sidecar-staleness bug and a real config-write-default CLI-override bug found during testing; documents remaining verification and architectural gaps |
| **v0.6.13.0** | Shipped | Fleet-wide crash recovery: every api-run session (including Desktop's own sidecar) now auto-restarts a crashed profile, not just the default one; a new `--server-id` flag lets an admin install a genuinely independent, unattended OS service per profile |
| **v0.6.14.0** | Shipped | Fixes a real, user-reported bug: the Console page showed nothing during a server Start/Stop/Restart. Two compounding causes fixed — the passive auto-refresh timer was gated off for the whole operation, and a server-side log-merge bug could silently evict the freshest log source entirely |
| **v0.6.15.0** | Shipped | Save-Data Edit Engine Foundation — a real Pal Editor (nickname, level, rank, IVs, gender, lucky flag), the top finding from a 21-repo competitive survey. Found and fixed a real ownership-resolution bug during its own live verification |
| **v0.6.16.0** | Shipped | Live World Map — real live player positions plotted on the Players page, sourced from data MystTiq already polls. Pal and base positions explicitly deferred, not yet exposed by any available data source |
| **v0.6.17.0** | Shipped | Two-Way Discord Bot Control — a real Discord bot with guild-scoped slash commands (start/stop/restart/status/players/broadcast/kick/ban), role-mapped to Discord roles, plus a fixed outbound Discord embed dispatch |
| **v0.6.18.0** | Shipped | Anti-Cheat & Save-Integrity Scanning — invalid Steam ID, impossible level, and Pal stat-anomaly detection, configurable Flag/Kick/Ban responses defaulting to Flag-only. Closes out the v0.6.15.0 survey's four-milestone sequence |
| **v0.6.18.1** | Shipped | Fix release: a code review of v0.6.15.0–v0.6.18.0 found and fixed three real logic bugs — a Pal-edit Gender verification check that could never fail, an anti-cheat cooldown key collision that silently dropped findings for a second anomalous Pal owned by the same player, and a Discord bot false-positive-disconnect risk in its bad-token detector |
| **v0.6.18.2** | Shipped | Server Setup page cleanup: removed a redundant hero card and the duplicate Check-for-Updates/Install-Missing buttons (Update Center already owns that functionality), and relocated First-Run Server Defaults to the new-connection-profile flow on the Settings page |
| **v0.6.18.3** | Shipped | Configuration page overhaul: quote-stripped Server Name/Description/passwords, auto-applying QoL presets plus locally-saved custom presets, Advanced Settings highlights non-default values live, Simple Settings expanded from 8 to all 34 curated settings, search/filter repositioned above the list and extended to Simple view, redundant header removed |
| **v0.6.18.4** | Shipped | Server Control ribbon buttons now grey out based on the server's real lifecycle state — Start disables while already running, Stop/Restart disable while already stopped, instead of staying clickable regardless of actual state |
| **v0.6.19.0** | Shipped | True multi-tab server connections — each open tab now owns its own live connection instead of tabs being a bookmark list over one shared connection; opening a tab either sets up a new server or connects to an existing one, and a profile already open in another tab can't be opened again; background tabs keep polling lightweight connection/status state, the active tab still drives full page data |
| **v0.6.19.1** | Shipped | Removed the ribbon toolbar groups that duplicated the left nav sidebar one-for-one within the same category (World/Server/Mods/Tools/System); Fleet page's Server Profiles list now shows a colored status dot per server, and a failed fleet-wide action now shows its actual error message instead of just a bare "False" |
| **v0.7.0.0** | Shipped | Themes, skins, and UI personalization: 4 selectable accent themes x true light/dark mode, applied live and remembered; a small hand-authored original vector icon set (paw print, wrench, capture-sphere, tent, shield, network, vault, monitor) replacing several generic system-font icons on the highest-visibility surfaces |
| **v0.7.0.1** | Shipped | User-reported: the Fleet page's left nav sidebar was entirely blank while actually on the Fleet page (the whole System category's nav list disappeared, and the System category tab lost its selected state) — `IsV5SystemCategory`/`IsSystemGroupSelected` never included `NavigationPage.Fleet` in their page checks, so navigating to Fleet failed its own category's visibility test |
| **v0.7.1.0** | Shipped | First slice of a broader UX pass: fixed 7 concrete page-ordering issues (World Map ahead of the player list it wasn't filtered by, duplicate Backup action buttons, Automation's creation form ahead of existing rules, resource stats buried below the audit log, Fleet's Clone-World-before-you-can-see-existing-IDs ordering, an undifferentiated "Restart Server" button, duplicate editable server paths on two pages), relocated the Pal Editor off the unrelated Guilds page, added a visible indicator when a curated Simple Setting isn't in the live INI instead of silently vanishing, tab bar now shows Local/Remote under the server name, and the "+" button is a plain plus sign instead of a themed icon |
| **v0.7.2.0** | Shipped | New-server creation now warns, live, if a typed Game/REST port is already bound by another process on the target machine — reuses the existing real `netstat`-backed port-listing capability (Windows + Linux) through a new standalone port-check endpoint, verified end-to-end against a real running sidecar and a real occupied port, not just statically |
| **v0.7.3.0** | Shipped | "Set Up New Server" is now a real 3-step wizard (Connection Details → In-Game Server Defaults with the v0.7.2.0 port warnings inline → Confirm & Finish) built entirely from existing fields/commands — editing an already-saved profile is completely unaffected, since every new step is gated behind "currently creating a new profile" |
| **v0.7.4.0** | Shipped | Card flare: every one of the 7 nav categories (Home/Server/World/Backups/Mods/Tools/System) now gets a consistent, subtle border-tint + soft glow across its own pages' cards (~169 cards tagged), reusing each category's existing tab-accent color family — a page now visually signals which part of the app it belongs to at a glance |
| **v0.7.5.0** | Shipped | No-scroll initiative, pass 1: fixed a real bug where the Settings page's Managed Server Configuration and Security cards rendered unconditionally underneath every new-server-wizard step instead of being hidden while creating a profile; tightened global card padding/control heights/page spacing (no content removed); the Players page's World Map and Pal Editor, Diagnostics Center's WAN Reachability and Local Machine Diagnostics, and Alert Center's Discord Bot and Anti-Cheat sections are now collapsed by default (one click to expand) since each is a large, secondary, occasional-use tool |
| **v0.7.6.0** | Shipped | Fixes a real safety bug: switching tabs didn't refresh player/log/dashboard data, so for up to 5 seconds a Kick/Ban click after switching tabs could fire against the WRONG server using the previous tab's stale player ID. Tab switching now clears any selected player/backup/mod/Pal/base immediately and triggers an out-of-cycle refresh instead of waiting for the next timer tick; a response for a tab the user has since switched away from is now discarded instead of overwriting the newly active tab's data; the "Set Up New Server" wizard step and profile edits/deletes are now correctly scoped per tab instead of shared/unguarded across all open tabs |
| **v0.7.7.0** | Shipped | Fixes real race conditions: a backup restore could previously run concurrently with a Guild/Base Ownership or World Transaction on the same save directory (the backup service never registered with the app's own operation-locking coordinator), and a diagnostics-triggered network restart bypassed that same lock entirely. Also fixes a restore that succeeds but whose cleanup step fails being misreported as "restore failed", a rollback-of-rollback path that could throw and get silently swallowed (leaving corrupted data live with the good copy stranded in a side folder), and a mod-snapshot rollback that skipped the ZIP path-traversal guard used everywhere else |
| **v0.7.8.0** | Shipped | RCON-powered admin tools verified against Palworld's actual RCON command set: Unban and a Ban List view (un-stubbing a button that had sat disabled in the UI), admin teleport (to a player / summon a player to you), and a one-click Save World Now. Also fixes a v0.7.6.0 follow-up correction: Kick/Ban/Unban/Teleport actually read a different, previously-unaddressed selection (`SelectedPlayerRecord`) than the one v0.7.6.0 fixed, with an even wider staleness window across a tab switch — found while wiring the new actions onto the same selection, fixed in the same release |
| **v0.7.9.0** | Shipped | Real server performance metrics: Palworld's official `/v1/api/metrics` REST endpoint now surfaces actual in-game simulation FPS and frame time on the Monitoring page, alongside (and independent from) the existing host-level CPU/RAM/thread sampling — the thing that actually degrades with base/Pal count and what operators watch for lag, which host CPU% alone can't show |
| **v0.7.10.0** | Shipped | Whitelist system: opt-in per-server allow list — when enabled, any online player not listed is automatically kicked, checked on the same poll cadence the player registry already uses. The last of this session's verified competitive feature gaps, and (like teleport in v0.7.8.0) named as an explicit deferred roadmap item in the codebase's own comments before this release |
| **v0.7.11.0** | Shipped | App lifecycle fixes: the window's own close (X) button previously always minimized to tray no matter what, with no way to fully quit that way — it now checks every open tab (not just the active one) and does a real, clean exit when nothing is running, otherwise minimizing to tray with a visible reminder that MystTiq is still managing a running server (Avalonia's tray icon has no built-in balloon-notification API, so this is a small self-dismissing window standing in for one). Closing a tab whose server is running now asks first — stop it, leave it running, or cancel — instead of always leaving it running silently. A second launch while MystTiq is already running is now blocked with a clear notice instead of opening a second GUI. Also fixes 4 stale "current"/"planned" labels found in `docs/roadmap/` that contradicted already-shipped work |
| **v0.7.12.0** | Shipped | Test coverage: a test-coverage audit found the v0.7.8.0–v0.7.10.0 features had zero automated regression coverage beyond regex-on-source checks. Adds a real object-graph harness for `HeadlessWhitelistService.EnforceAsync` (the one piece of business logic that had never actually executed — 6 scenarios covering enable/disable, kick vs. allow, re-kick dedup and its reset, and an unavailable snapshot) and a permanent route-smoke script covering every previously-untested v0.7.8.0/v0.7.9.0/v0.7.10.0 REST route. Also fixes a stale code comment in `ProviderModels.cs` still listing whitelist/teleport as deferred after both shipped |
| **v0.7.13.0** | Shipped | "Set Up New Server" is no longer embedded in the Settings page's card stack alongside regular editing controls — it's now a dedicated full-screen host that replaces the nav sidebar/ribbon/category tabs entirely while a tab is mid-setup, modeled on how connection-manager apps (SSH/database/remote-desktop clients) keep "New Connection" a focused, separate sequence. Adds a new first step choosing Local or Remote before Connection Details, each showing only the fields relevant to that choice (Local: name + auto-detect-or-manual service URL; Remote: LAN scan + name/URL/token/certificate pin). Verified live against 3 simultaneous real sidecar instances — two ordinary loopback local servers plus a third rebound to the LAN interface with authentication and TLS enabled, confirming unauthenticated requests are correctly rejected and the machine's own local network address is reachable exactly as a genuine remote connection would be |
| **v0.7.14.0** | Shipped | First direct pass against Fluent/Avalonia UI best practices (previously only page-ordering/card-flare/no-scroll/wizard work had happened). A dedicated audit against current Microsoft Fluent guidance and Avalonia's own accessibility docs found and fixed 4 real gaps: zero `AutomationProperties.Name` anywhere in the app despite 50+ tooltips (icon-only controls were silent to screen readers), a missing keyboard focus ring on ghost/ribbon/category-tab buttons (their own always-on border color was winning a style-specificity tie against the app's focus-visible style), a 20x20px tab-close button below Fluent's ~32-40px minimum pointer-target guidance, and missing tooltips on the 3 window-chrome buttons. A 5th audit finding (no busy/disabled feedback during async actions) was investigated and found to already be correctly handled everywhere it named — via each command's own `CanExecute` gate, not the XAML pattern the audit's grep checked for — so nothing was changed there |
| **v0.7.15.0** | Shipped | Backlog feasibility pass: a research spike found 2 of 5 longstanding backlog items genuinely buildable now, 1 was an orphaned stub with no real backend target, and 2 (give-items/give-Pals, mod load-order editing) remain blocked or open-ended and were left untouched. Ships real Temporary Bans (a duration-based ban that auto-lifts via the existing ban/unban RCON path, mirroring v0.7.10.0's Whitelist pattern) and real historical FPS charting (extends the existing 30-day metrics history and its chart control with the server FPS data v0.7.9.0 already collects live). Also removes a stale "Whitelist — BACKEND REQUIRED" stub button left behind since Whitelist itself shipped, and removes the "Remove Admin" stub outright — Palworld has no per-player admin concept for it to ever target |
| **v0.7.16.0** | Shipped | Tab strip is now actually responsive: it previously had a hardcoded 720px width cap regardless of how much window space was really available, and any tabs that didn't fit were silently clipped with no way to reach them. Tabs now use the real measured width of their host panel, and once there are more open than fit, a new chevron button reveals the rest in a flyout (click to switch) with "Set Up New Server" always available there too — modeled on how browsers handle tab overflow. The active tab is never the one that gets hidden |
| **v0.7.17.0** | Shipped | Fixes the `api-remote-enable` CLI bug found (but explicitly deferred) during v0.7.13.0's live remote-connection testing — a blanket pre-command validation rejected the one command whose entire job was to satisfy it, making the documented token→cert→remote-enable workflow non-functional. Also restores the Linux packaging/deployment pipeline, broken since v0.6.1.0 for lacking a version-specific acceptance-script pair; rebuilt and expanded to cover the full current read-only/safe API surface, verified against a real Linux VM (118/118), surfacing and fixing 5 real bugs along the way (a runtime-root permission crash, a boot-scoped journal-scan false positive, 3 wrong-status-code test expectations, a null-reference crash on an optional config field, and a JSON-deserialization type bug in backup retention filtering) |
| **v0.7.18.0** | Shipped | A selectable "Hide duplicate names" filter on the Players page's Directory list — when the same physical player shows up more than once under the same display name (a rejoin under a different platform ID, a stale record from an old save), only the best candidate (online, then has a save, then most recently updated) stays visible. Purely a view-side filter: nothing is deleted or merged, and unchecking it always restores every record |
| **v0.7.19.0** | Shipped | Every one of the app's 25 nav-sidebar destinations now has a real, polished icon — closing a gap open since v0.7.0.0, which hand-authored only 7 (Dashboard/Server Setup/Players/Bases/Backup Center/Security/Fleet), leaving the other 18 on plain Unicode-glyph text prefixes (⚙, ▰, ♛, ◆, and so on). A full hand-authored "glass orb" icon set — glossy gradient spheres with a distinct glyph and accent color per destination — replaces both the old vectors and the glyph prefixes uniformly |
| **v0.7.20.0** | Shipped | The Players page's World Map card gets two bundled preset backgrounds (Palpagos, the base game map, and World Tree) alongside the existing "browse for your own image" option — no more hunting down and cropping your own Palworld map screenshot before the map has any geography-shaped context. Player dots still spread relative to whichever players are currently online, same as before; this release is presets only, real coordinate calibration is a separate, deliberately-isolated next step |
| **v0.7.21.0** | Shipped | An opt-in "Experimental: real-world positions" toggle for the Palpagos preset, using a numerically-verified formula (reproduces its open-source origin project's own published worked example exactly) to place player dots at their true in-game position instead of just their spread relative to other online players. Off by default and Palpagos-only: the formula's math is verified, but the map image's own origin/orientation isn't independently confirmed yet, so this stays opt-in rather than replacing the existing view until a user can confirm it against a real server |
| **v0.7.22.0** | Shipped | The footer's "Working…" indicator now names what's actually running — server start/stop/restart, backup create/restore, and world transaction apply all set a real reason text instead of the same generic label regardless of which of the app's many possible operations triggered it. Other operations not yet threaded through keep the generic label, a disclosed gap rather than a claim of full coverage |
| **v0.7.23.0** | Shipped | A background tab's connection-check previously collapsed every failure — an incompatible API version, a missing bearer token, an actually-unreachable server — into the exact same "Connection failed" text, discarding the real reason via a bare `catch`. Now mirrors the same specific exception handling the active tab's own connection check already had, so switching to a background tab that failed to reconnect shows what actually went wrong |
| **v0.7.24.0** | Shipped | Roadmap documentation audit: `docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md`'s "Service-style watchdog/recovery behavior" row had sat labeled "Discovery backlog" since v0.6.13.0 actually shipped `HeadlessFleetCrashRecoveryService` — now correctly marked Shipped with a citation. The neighboring "Improved structured log rotation/retention" row was investigated rather than assumed: confirmed genuinely still open on both platforms (`HeadlessConsoleLogWriter` appends indefinitely with no cap, rotation, or retention anywhere), so it stays in the backlog, now with that confirmation recorded instead of an unverified guess |
| **v0.7.25.0** | Shipped | First release of a broader GUI/workflow/UX overhaul requested directly, gathered page-by-page into a 21-version roadmap. This release lays the foundation the roadmap's later ribbon-relocation versions depend on: the ribbon (ConnectCommand/StartCommand/etc. action buttons under the category tabs) is now data-bound instead of hand-authored per group, and shrinks to fit the actual window width the same way the tab strip already does (v0.7.16.0) — groups that don't fit move into a "»" overflow flyout instead of being silently clipped |
| **v0.7.26.0** | Shipped | First wave of ribbon relocations, made possible by v0.7.25.0's adaptivity work: Server Setup's "Verify Files" button, Configuration's Import/Export/Save Changes/Reset Unsaved (with the redundant "Load Active" button removed entirely), and Console's Refresh View/Pause/Clear/Export all move into the ribbon as new per-page groups. Console's RCON card also moves above the Console Log card |
| **v0.7.27.0** | Shipped | Second wave of ribbon relocations: Workspace's Refresh, Backups' Create/Verify All/Refresh/Open Backup Root, MOD Dashboard and MOD Library's shared Refresh MODs/Verify & Scan, UE4SS's Refresh Runtime, and Server Doctor's Run Doctor/Export Report all move into the ribbon as new per-page groups |
| **v0.7.28.0** | Shipped | Three new ribbon capabilities, not relocations: Force Stop Server (wires up `ForceStopServerAsync`, which already existed but was only ever called internally on app exit) reachable from every page; Install Missing on Server Setup (wires up `InstallMissingEnvironmentAsync`, previously only reachable indirectly through a per-row action); and a managed-process list plus Kill Processes on Server Doctor, surfacing `ServerStatusDto.Processes` data that already flowed end-to-end from the server but was never read on the Desktop side |
| **v0.7.29.0** | Shipped | Two confirmed bugs fixed: selecting the "Official / Vanilla" config preset no longer wipes Server Name/Description/passwords — it was resetting every Simple-Settings-view field, including identity, instead of just gameplay rates like the Balanced/Relaxed presets already correctly did. And a genuinely-running PalServer at an unexpected install path no longer silently reports as "Stopped" with no explanation — the backend now distinguishes "not running" from "running, but not at the configured path," and the Dashboard's SERVER/OVERALL HEALTH cards (which previously discarded this detail entirely) now show it |
| **v0.7.30.0** | Shipped | Three more confirmed bugs fixed: Backups' summary cards always reading 0 (two of three places that refreshed the backup list forgot to notify the summary counts — now unified through one shared helper so it can't drift a fourth time); UE4SS reporting the meaningless placeholder "0.0.0.0" as its installed version (an unstamped DLL's version resource can legitimately return that literal string); and Server Doctor's unexplained "UNKNOWN" label (the backend's own explanation — e.g. "Server is not running; no health issues detected." — was already being computed, just never read on the Desktop side). A fourth suspected bug (UE4SS "Unverified"/"Not reported") turned out, on closer investigation, not to be one — see the architecture doc for the correction |
| **v0.7.31.0** | Shipped | New-server-setup safety: two tabs pointed at the same physical server install now get a visible warning on the Dashboard, even if they're connected via different ports (e.g. two remote profiles that happen to share a port) — identity is the install location a connected server reports about itself, not the address you connected through. Since a saved connection profile has no concept of install location before you actually connect to it, this surfaces as a warning banner once the match is discovered rather than blocking the setup wizard beforehand |
| **v0.7.32.0** | Shipped | Visual density pass: nav-pane icons are noticeably larger (20px → 28px, plus the column that holds them); Server Setup's 4 summary cards, Backups' 4 summary cards, and Server Doctor's per-check cards are all more compact (tighter padding, smaller text, shorter labels) so more fits on screen without scrolling; MOD Library's 6 summary cards (which duplicated MOD Dashboard) are removed from that page; Server Setup's READY count is centered. Also fixed a carried-over oversight: v0.7.26.0 moved "Verify Files" into the ribbon but never removed the original embedded copy — removed now |
| **v0.7.33.0** | Shipped | Home Dashboard fixes: removed the ONLINE PLAYERS card that duplicated the top-row PLAYERS summary card's OPEN-to-Players-page role, letting the LIVE SERVER / MANAGER LOG card go full width in its place; the BACKUP card now gets the amber `glowAmber` tint plus an OPEN button (matching the ACTIVE WORLD card's pattern) instead of the generic `accentHome` styling with no action; and fixed the real cause behind sparse console output at server start — every log-bearing status/log-tail call site requested only 120 lines against the headless service's own 500-line cap, starving each merged source (MystTiq stdout, Pal.log, AdminCommands logs) down to as little as ~40 lines apiece — raised to the server's actual max everywhere it matters (initial tab connect, the recurring per-tab refresh timer, the shared monitoring-refresh core behind manual/Console-page refresh, and both the mid-operation and post-operation log polls around Start/Stop/Restart) |
| **v0.7.34.0** | Shipped | Category tab checked/hover gradients were swapped: selecting a category tab now settles into the darker glass look while hovering shows the brighter gradient, the reverse of before and now matching the file's own "Category selection is dark glass" comment. The navigation pane's background changed from the flatter `NavGlassSurfaceGradient` to the same darker glass-hover gradient used on hover across ribbon buttons, category tabs, and nav buttons elsewhere in the app |
| **v0.7.35.0** | Shipped | Two new semantic button color classes, designed once and applied everywhere they're relevant instead of one-off per page: `inspectAction` (cyan) for non-mutating "look at/confirm" actions (Workspace's 4 Browse buttons, Server Setup's VERIFY/RESCAN row actions, Configuration's Generate button — now bigger and labeled "🎲 Generate" instead of an icon-only button) and `targetAction` (violet) for "act on a specific thing" actions (Workspace's 10 Open-in-explorer buttons, Server Setup's MANAGE/INSTALL/CREATE/ENABLE row actions). Neither reuses the Save/Start gradient, so every previously-identical default-gray button now reads as what it actually does |
| **v0.7.36.0** | Shipped | The title bar and footer status bar were the only two primary chrome surfaces still using a hardcoded hex background/border instead of a theme-aware resource — both now use the already-defined, already theme-registered `StatusSurfaceGradient`/`BorderSoftBrush` (previously unused for this purpose despite existing specifically for it), so Light mode and the three accent themes now actually reach them instead of leaving them dark navy while the rest of the chrome re-themes. The separately-disclosed BoxShadow/decorative-glow color ceiling (a real Avalonia platform limitation, not an oversight) is unchanged |
| **v0.7.37.0** | Shipped | Backups page rework: the redundant "N backup(s) / total size" header text is gone (already covered by the TOTAL ARCHIVES summary card below it); the page is now a two-column layout with the backup table on the left and the Selected Backup / Retention Cleanup action cards stacked in a narrower right-hand column, mirroring the v0.2.16.4 reference (its third right-column card, "Backup Locations," has no equivalent left to move — its one action already moved into the ribbon in v0.7.27.0); and the table's Created column now leads instead of the filename |
| **v0.7.38.0** | Shipped | Configuration page rework: the preset/unsaved-changes/validation notification strip moved from directly under the Simple/Advanced toggle row down to sit just above World Settings (renamed from "Gameplay Rates"), which is now split into three labeled sub-sections (World, Player & Pal, Items & Work) instead of one flat 22-row list; Advanced Settings now keeps the same Server Identity and Network/Access & Limits cards Simple view has, swapping in the full OptionSettings table only for the content below them, instead of a totally separate bare-table layout; the Generate button fix from v0.7.35.0 (bigger, labeled "🎲 Generate") carries forward unchanged, already covering item 27 |
| **v0.7.39.0** | Shipped | MOD installation now detects PAK vs. UE4SS from the archive's own extracted contents (any `.pak`/`.ucas`/`.utoc` file anywhere in it means PAK, otherwise UE4SS) instead of trusting a manual dropdown that defaulted to PAK and silently installed a UE4SS mod under the wrong type whenever a user forgot to flip it — the dropdown is gone from MOD Library's Install Validated ZIP card, and `HeadlessModManagementService.InstallZipAsync` now decides for itself after extraction, regardless of what the client requests |
| **v0.7.40.0** | Shipped | MOD Library reworked to a 3-column layout matching the v0.2.16.4 reference: Installed MODs and Available Local Steam Workshop Mods now sit side-by-side (previously three full-width cards stacked vertically) with a new MOD DETAILS panel alongside them — Overall Health, Installation, Runtime, Compatibility, and Evidence for whichever MOD is selected in the Installed MODs list, something nothing rendered before this release. Install Validated ZIP moved to the top of the page, above the lists. The website-sourced MOD description part of the original finding stays explicitly deferred, unrelated design work |
| **v0.7.41.0** | Shipped | New "Check for Update"/"Update" actions in MOD Library's details panel, deliberately kept out of the Update Center per the original finding. Scoped to what's actually verifiable without a live network call: for a MOD matching a locally-scanned Steam Workshop item, the check compares the installed files' timestamps against Steam's own local Workshop content cache — real, on-disk evidence that Steam already downloaded an update, not a fabricated "latest version" claim. MODs with no matching local Workshop source get an honest "no known update source" message instead. Update reuses the existing, already-tested Workshop-import route unchanged |
| **v0.7.42.0** | Shipped | Cross-page consistency sweep (direct mid-session request): applied the ribbon-relocation pattern from v0.7.26.0-v0.7.28.0 to the 13 remaining pages that still had page-header action buttons in-page — World Explorer, World Transactions, Guilds/Bases, Players, Network Diagnostics, Save Tools, Notifications, Automation, Alert Center, Security, Fleet, Crash Analyzer, and Update Center. Also fixed two real issues found along the way: Network Diagnostics' "Re-run Network Tests" was a literal duplicate of "Run Diagnostics" (removed, not relocated), and its "Open Server Log" button was redundant with the always-present global ribbon Quick Actions "Console" button (removed). Sub-feature-local actions inside a labeled workflow card (Fleet Actions, World Transactions' confirm-gated Repair Center, Alert Center's Discord Bot/Anti-Cheat refreshes) stayed in-page, matching the existing Backups Retention Cleanup precedent |
| **v0.7.43.0** | Shipped | Findings-completeness audit (direct mid-session request to verify earlier work actually shipped) found and fixed two real gaps: MOD Dashboard never got the Installed-MODs-list-plus-details-panel work v0.7.40.0 only applied to MOD Library, despite the original ask (items 21 and 40) explicitly wanting it on Dashboard too — added a read-only copy there, matching the page's own "Open MOD Library to change state" framing; and the footer's "working on" indicator (item 6) had no version slot anywhere in the 22-version roadmap and no elapsed time — added a live elapsed-time ticker and which server the operation belongs to. Also doubled the nav pane icons again (28×28 → 56×56, direct mid-session request), growing the nav row 58→72px and the icon column 32→60px to fit without clipping |
| **v0.7.44.0** | Shipped | Palworld Instance Detection & Termination Tool (direct mid-session request), motivated by a real collision hit twice during this session's own release verification. Server Doctor gains an "ALL PALWORLD INSTANCES ON THIS MACHINE" panel listing every Palworld process on the machine — not just this tab's own managed one — via a new machine-wide scan (`IServerLifecycleService.FindAllInstancesAsync`, both Windows/Linux) built on the existing raw `FindProcessesByName` primitive. Selecting an instance shows a MOD-DETAILS-style panel: an instance confirmed as this tab's own managed process gets the existing safe, crash-recovery-aware Stop; an unconfirmed one gets a disclosed-risk "Force Kill (Unmanaged)" raw termination, with an explicit warning that MystTiq cannot know whether that PID belongs to a different local MystTiq session's own crash-recovery tracking — killing it directly may look like an unexpected crash there and trigger an auto-restart, instead of a clean stop |
| **v0.7.45.0** | Shipped | Update Center Overhaul (item 51) — the full component-by-component version-tracking table from the v0.2.16.4 reference, all 11 components in one pass per direct instruction, including MystTiq's own self-update awareness (GitHub releases, informational only — no auto-install). New `HeadlessComponentUpdateService` gives every component real installed/latest version tracking: Core Server (MystTiq, SteamCMD, Palworld Dedicated Server via the unauthenticated Steam Web API UpToDateCheck endpoint, UE4SS Runtime via GitHub) and Save & Runtime Dependencies (Python, pip and Palworld Save Tools via PyPI, PlM/Oodle Decoder, .NET Runtime via the official dotnet/core releases index, VC++ Runtime, MSVC Build Tools). Several components genuinely have no reliable unattended "latest version" source (Python, VC++ Runtime, MSVC Build Tools, PlM/Oodle Decoder — no single canonical upstream project) — those honestly report "Unavailable"/"Check manually" instead of a fabricated comparison, matching this project's established disclosed-gap convention |
| **v0.7.46.0** | Shipped | Console Newest-First Ordering (direct live bug report against the user's real running server): the Console page's log view now shows the most recent activity at the top instead of the bottom — `FilteredLogLines` is now built from `LogLines.Reverse()`, with the underlying chronological order preserved for the Dashboard's Live Activity panel. Surfaced during the same investigation: root-caused and fixed (live, via the running server's own `/config/editable` API) a real "Operation failed"/won't-connect bug — the profile's launch arguments never had `-port=8211`, so PalServer silently bound Steam's default UDP 27015 instead of the configured port, and MystTiq's own readiness check only ever polled 8211, so Start/Restart always timed out even though the process was healthy. Also root-caused, but not yet fixed, a second real gap: Console doesn't capture ongoing PalServer/UE4SS output because MystTiq only redirects stdout from the `PalServer.exe` wrapper it directly launches, not the `PalServer-Win64-Shipping-Cmd.exe` grandchild that does the actual engine work — investigated further and scoped (see v0.7.47.0) |
| **v0.7.47.0** | Shipped | UE4SS Release Catalog (item 45, listing half). Investigated three candidate console-capture fixes live, in an isolated copy of the user's real server, and ruled all three out (`-ABSLOG` produces no file at all; launching the grandchild process directly still captures zero output; UE4SS's Lua API has no engine-log hook) — the real fix needs native DLL proxy injection, scoped as its own future project, not attempted here. Picked up the next roadmap item instead: the UE4SS page's "Release source" dropdown gets real data for the first time, replacing its permanent "BACKEND REQUIRED" stub. New fleet-level `GET /api/v1/ue4ss/releases` route lists real releases for both sources (`Okaetsu/RE-UE4SS` Palworld Fork: 2 releases; `UE4SS-RE/RE-UE4SS` Official Upstream: a real multi-release history), each with its correct install asset picked out from multiple attached files per release (excluding developer/`-zDev` builds), verified against real GitHub data for both repos before writing any code. Listing-only — install/rollback is separate, higher-risk work deferred to its own future version |
| **v0.7.48.0** | Shipped | Central Theme System Completion, Foundation Pass (direct live bug report: "have a central place where a variable can be changed that sets the theme colour so that the background and buttons all change... light mode... text colour... background colours and images"). Also fixed, per direct request: Update Center's UE4SS row was reading a stale GitHub tag following v0.7.47.0's own discovery that the fork changed release strategy — now always picks the most recently published release. A real central theme mechanism (`ThemeCatalog.cs`/`ThemeApplier.cs`) already existed but was badly incompletely wired: 26 hardcoded colors in `MainWindow.axaml` and ~300 in `DesignSystem.axaml` never touched it. Grounded against a full read of both files: most of those 300 turned out to be per-page accent tints (Server=blue, World=cyan, Mods=magenta, etc.) and status glows that already, by construction, duplicate the catalog's own colors as raw hex instead of referencing them. Scope decision, asked and answered directly: per-page tints now shift with the chosen accent theme, not just Dark/Light. Built a systematic, formula-driven derivation engine (`ThemeColorMath.cs` + new `ThemeApplier` logic computing border/glow/card-gradient resources for ~11 base colors, in every theme and variant) rather than hand-authoring ~300 x 4 x 2 combinations blind. Also discovered and closed a real gap: the "BoxShadow can't use DynamicResource" limitation disclosed since v0.7.36.0 only applies to the shorthand-string syntax — binding the whole attribute to a `BoxShadows`-typed resource works, verified via a real build. Shipped: the full page-accent system, every status glow, category tabs, ribbon context colors, status badges, mod/backup cards, all MainWindow.axaml structural chrome (3 terminal-style consoles deliberately kept dark, disclosed), and the v0.7.35.0 button-color system's own previously-unwired Inspect/Target gradients. ~240 more hardcoded colors remain — mostly elaborate translucent "glass" gradients needing real design judgment this session can't verify visually — deferred, and pushed to the end of the 0.7.x line at direct request (2026-09-10) so the remaining new-capability roadmap items ship first. Nav icons and background art confirmed as wanted but out of scope for this pass (raster images, no vector source; real theme-awareness needs asset regeneration or runtime hue-shifting) |
| **v0.7.49.0** | Shipped | UE4SS Install/Rollback (item 45, install half — v0.7.47.0 shipped listing-only). Verified live against real release zips from both catalog sources before writing any install code: `Okaetsu/RE-UE4SS` packages the modern `ue4ss/` subfolder layout (`dwmapi.dll` at the zip root, everything else under `ue4ss/`); `UE4SS-RE/RE-UE4SS` packages the legacy flat layout (`dwmapi.dll`/`UE4SS.dll`/`UE4SS-settings.ini`/`Mods/` all at the zip root) — genuinely different packaging, not a naming variation. Rather than guess a flatten/nest transform between them, install faithfully mirrors whichever layout the selected zip itself uses onto the server's binaries folder — exactly what each project's own install instructions already tell a user to do by hand — and refuses to install a release whose layout doesn't match an existing install (protects against UE4SS loading twice). Any zip entry under a `Mods` folder at any nesting level is skipped outright, so `mods.txt`/`mods.json` and every installed MOD folder are never touched by an engine install; `UE4SS-settings.ini` is preserved if it already exists on disk. New `HeadlessModManagementService` methods (`PreviewUe4ssInstallAsync`/`ApplyUe4ssInstallAsync`/`RollbackUe4ssInstallAsync`) follow the same token-based Preview-then-Apply shape already established by backup retention cleanup: Preview resolves the selection against the live GitHub catalog server-side and returns a short-lived token; Apply only proceeds against that exact token, requires PalServer stopped (its engine files are loaded into the running process), takes an automatic pre-install snapshot of only the exact files about to be overwritten, and rolls back automatically on any mid-install failure. A standalone Rollback command restores that same snapshot on demand. New routes: `GET /ue4ss/install/status`, `POST /ue4ss/install/preview`, `POST /ue4ss/install/apply`, `POST /ue4ss/install/rollback`. Desktop: the UE4SS page's release list is now a selectable `ListBox` wired to three new ribbon buttons (Preview Install, Confirm Install, Rollback), replacing the permanent "isn't wired up yet" stub from v0.7.48.0. Exercised live end-to-end via a throwaway harness against real GitHub release data (21/21 assertions passed) before shipping |
| **v0.7.50.0** | Shipped | Console Source Completeness (UE4SS.log) — found while scoping Native Console Capture (moved to the end of the 0.7.x line, see below), shipped first since it's real, low-risk, and immediately valuable. Checked the long-standing "Pal.log" console source directly against this machine's real, actively-modded local Palworld install rather than continuing to assume it: **confirmed live that Pal.log has never existed anywhere in that install** — consistent with `-ABSLOG` also producing nothing. Meanwhile `ue4ss/UE4SS.log` (real, substantial content — 1947 lines from one session alone) was never in `HeadlessMonitoringService.ResolveConsoleSources`'s candidate list at all. Added it (plus its legacy-layout path). Verified live, read-only, against the real install via a throwaway harness with an isolated runtime root (never touching the live production instance) — confirmed real UE4SS content now merges in, and surfaced that the already-existing "AdminCommands server log" source carries genuine player connect/disconnect narration with real names/timestamps that was always reachable but easy to miss given how thin the console looked without UE4SS.log alongside it |
| **v0.7.51.0** | Shipped | Per-Tab Color Coding (item 2) — each open tab gets a persistent identity color, one of 10 base colors the v0.7.48.0 theme derivation engine already produces resources for, assigned once at profile creation (round-robin) and reused on every reconnect rather than re-derived from tab position. Rendered as a small colored stripe in the tab strip, kept deliberately separate from the health-status dot and the selected-tab highlight — three distinct signals, not merged |
| **v0.7.52.0** | Shipped | "+" Flow Restructure (item 53) — the "+" flyout goes from one generic "Set Up New Server" entry to four explicit top-level choices: Set Up New Server (unchanged), Connect to Local Server, Connect to Remote Server (both reuse the existing wizard's Local/Remote choice, previously one step deeper), and Clone a Server (new — routes to the existing Fleet-page Clone World feature, only shown when an eligible connected local tab exists to clone from). No new backend logic; reuses existing wizard/clone commands throughout |
| **v0.7.53.0** | Shipped | Dashboard Layout Density (item 13) — smaller/denser stat cards on the Dashboard, matching the same padding/font-size treatment the v0.7.32.0 density pass already applied to Server Setup/Backups/MOD Library/Server Doctor. Structural changes from the v0.2.16.4 reference (inline ribbon buttons, collapsible-tree nav) are a different, larger change and explicitly out of scope — this is card density only |
| **v0.7.54.0** | Shipped | Per-Page Title Background Artwork (item 8) — real illustrated artwork behind the page header, Dark/Light aware, for the Home and World category tabs (generated via a free, no-login Bing Image Creator session driven through the browser; hit its daily anonymous-guest generation limit after 4 images, so the other 5 categories — Server/Backups/Mods/Tools/System — are deliberately left without art rather than shipping a placeholder). A genuinely new capability for this session: real AI-generated illustration, not a code-generated gradient/tint |
| **v0.7.55.0** | Shipped | Website-Sourced MOD Descriptions (item 40's deferred half) — MOD Library's DESCRIPTION panel can now fetch a real description on demand: Steam Workshop items via a real Steam Web API call keyed off the same local-content match Update Detection already uses, or a GitHub repository description when the user sets a manual Source URL for a MOD with no Workshop match (e.g. most UE4SS/Lua mods). Designed for multiple sources up front per direct instruction, not Workshop-only. Fetches are strictly on-click — never automatic or background — and results are cached to disk so repeat views don't re-fetch. Outbound calls are limited to Steam's and GitHub's public, unauthenticated, read-only REST APIs |
| **v0.7.56.0** | Shipped | Central Theme System Completion, Remaining Decorative Gradients — the ~240 hardcoded colors left after v0.7.48.0's foundation pass, overwhelmingly the multi-stop translucent "glass" gradients (GlassOptionHoverGradient, Primary/Success/DangerGlass, the nav sidebar's glass states, Context/Backup/GraphFill families). Computed via new formula-driven helpers in `ThemeApplier` rather than hand-authored per (color × theme × variant) combination — the same discipline v0.7.48.0 established, extended to the harder case of translucent-over-variable-background gradients. Five confirmed-dead legacy prototype styles (`modRow`/`prototypeRow`/`worldTabHeader`/`disableAction`/`updateAction`) left untouched rather than themed, since none are reachable from current navigation |
| **v0.7.57.0** | Shipped | Native Console Capture, Proxy DLL Foundation — the real, working half of the PalServer DLL-proxy logger investigated back in v0.7.51.0. A new native C++ artifact (`native/MystTiqConsoleProxy/`, built via `scripts/Build-ConsoleProxy.ps1`, outside the .NET solution) that proxies `DSOUND.dll` — confirmed by exact-ordinal comparison against the real system DLL and against PalServer's own import table — forwarding all 6 real exports so the game behaves identically, plus a DllMain lifecycle log. Verified by actually loading the built DLL in isolation: resolves the real DLL, forwards exports correctly, unloads cleanly. The actual Unreal log-capture hook is deliberately NOT implemented — it needs a live memory signature scan this session has no safe way to verify, and re-confirmed this session that the binary's Unreal log category strings are still stripped (`NO_LOGGING`), so the realistic payoff is small. Not wired into the app's install/deploy flow — ships as a standalone, disclosed-experimental artifact |
| **v0.7.58.0** | Shipped | Website-Sourced MOD Descriptions, Light Polish — three real rough edges found on review of v0.7.55.0's DESCRIPTION panel: long descriptions were silently clipped past a fixed height instead of scrolling (now wrapped in a `ScrollViewer`); the Source URL was inert text with no way to actually visit it (now has an Open button using the same `Process.Start`/`UseShellExecute` pattern already established elsewhere in the app); and the "No known Workshop match" guidance showed even before the user had ever clicked Fetch, not just after a real fetch came back empty (now gated on a new `ShowNoModDescriptionMatchMessage` computed property requiring both a completed fetch attempt and no match found) |
| **v0.7.59.0** | Shipped | Safe-Start MOD Diagnostic — direct request, grounded in real research: "remove mods one by one until the crash stops" is the standard, documented community troubleshooting technique for Palworld server crashes; this automates it. New `HeadlessModSafeStartService`: disables every enabled MOD, confirms the server actually starts clean with none of them (aborting immediately as "not a MOD problem" if even that fails, rather than false-flagging every MOD), then re-enables candidates one at a time on top of the confirmed-good set, testing for BOTH a crash and a startup hang (the exact failure mode this session's own live investigation hit) after each. Any MOD that fails either check is disabled again and the run continues; the surviving set is left running at the end. A new live-progress card in MOD Library polls status independently of `IsBusy` since the whole diagnostic runs over several minutes. Held for its full duration under the same `OperationCoordinator` lock Start/Stop/Restart use, so nothing else can race a live start/stop cycle mid-diagnostic |
| **v0.7.60.0** | Shipped | Port Conflict Prevention & UE4SS Version Tracking — two direct requests. (1) `WindowsServerLifecycleService`/`LinuxServerLifecycleService.StartAsync` now checks whether the configured UDP game port is already bound by anything (via the existing per-profile-aware `GetGuardedListeningPorts()`) BEFORE launching, refusing with a new `PortConflict` exit code and a clear message instead of letting PalServer start, spin up its full engine thread pool, and then sit forever unable to bind — a state genuinely indistinguishable from a hang without deep diagnosis, discovered directly while investigating this session's own unresolved freeze. (2) UE4SS version comparison in Update Center always reported "Check manually" regardless of what was actually installed, because there was never a reliable source for the real installed version — this fork ships no version marker file and the DLL's own `FileVersionInfo` is unstamped. `ApplyUe4ssInstallAsync` now records exactly which release catalog entry it applied (a small manifest under `ManagerRuntimeRoot`, invalidated on Rollback); when present, Update Center does a genuine exact-tag comparison and can report real `Up to date`/`Update available` status; falls back to the same honest "Check manually" when no manifest exists (pre-existing installs, or files copied in manually — bypassing MystTiq's own install flow) |
| **v0.7.61.0** | Shipped | PalServer Launch Freeze Root Cause Fix — the actual root cause of the PalServer launch freeze investigated on and off since v0.7.57.0, found live via a Process Explorer thread-stack capture on a genuinely frozen production process: the blocked thread sat in `USER32.dll!MessageBoxW`. PalServer, launched without Unreal's `-unattended` flag, can pop a native Win32 message box on certain Unreal/Steamworks conditions; since MystTiq launches it with no interactive desktop session, that dialog is invisible and unclickable, and the thread — and the whole server — waits forever. Zero CPU growth, no crash, no port bind, nothing in any log: every symptom chased across the injection chain, SteamCMD, Windows Defender, Hyper-V networking, and file corruption for weeks, caused by one missing launch argument. Confirmed live: the identical production server and its clone, launched directly with `-unattended` added, both blew straight past the point they'd always frozen at (UE4SS fully hooked, mods loaded, PalDefender started, sustained real CPU work) instead of sitting frozen. `-unattended` added to both platform default launch-argument templates (`HeadlessConfiguration.CreateWindowsDefault`/`CreateLinuxDefault`), and `HeadlessConfigurationService.LoadOrDefault` now backfills the flag into every already-persisted profile on load if it's missing, so existing installs are fixed automatically rather than needing a manual edit |
| **v0.7.62.0** | Shipped | Dashboard/Ribbon Layout Overlap Fix — found live: after certain reconnect sequences, the ribbon and category tabs went visually blank on every page, not just Dashboard. Confirmed via live process inspection (Sysinternals `cdb`/SOS, non-invasively attached to the running app) that the underlying data was completely correct at the exact moment the bug was visible on screen — `VisibleRibbonGroups` held its normal 3 groups, `_ribbonWidth` was a healthy real value, `SelectedPage` was correct — ruling out every ViewModel/data explanation and pointing squarely at pure Avalonia layout/rendering. The page-header panel (title/subtitle, top-right) is `HorizontalAlignment="Right"` with a `MinWidth` but no `MaxWidth`, inside a Grid with no `ColumnDefinitions` — nothing but that alignment constraint keeps it confined to the right portion of the same row the category tabs and ribbon occupy. Reported symptom matched exactly: category tabs "look like they're in the background." A `MaxWidth="620"` ceiling bounds the panel regardless of whatever Avalonia Arrange-pass condition was letting it expand, without a larger, riskier restructuring into real Grid columns. Confirmed fixed live on both Dashboard and Inspector pages |
| **v0.7.63.0** | Shipped | Central Theme System Audit: Status-Color Bypass Fix — user-reported live: the theme system was supposed to control every resource centrally, but the tab-bar status dot, Dashboard health label, Fleet server list's status dot, and Update Center's component table stayed locked to one color through every accent-theme/Light-Dark switch. Root cause: each was a hardcoded hex literal computed in C# (a ViewModel/Model property or a cached `IValueConverter` brush) bound via plain `{Binding}` rather than `{DynamicResource}` — a different bug class from the ~530-hardcoded-gradient sweep v0.7.48.0/v0.7.56.0 actually covered. Fixed by generalizing the correct pattern `TabSession.AccentBrush` (v0.7.52.0) already established: resolve the brush live via `Application.Current.TryGetResource` instead of caching it, through a new shared `SemanticStatusColorConverter`. Also found and fixed: the bare (non-accent) `Border.statuscard` style, used by the sidebar's mini status card and the duplicate-install warning banner, had no themed override at all — `App.axaml`'s pre-DesignSystem-era hardcoded style was the only one that ever applied. `App.axaml`'s entire dead legacy parallel color system was removed as part of the cleanup |
| **v0.7.64.0** | Shipped | Roadmap-Wide Gap Audit and Fixes — cataloged ~75 open items across every disclosed gap in this project's own history (release notes, architecture docs, roadmap files, TODO/stub markers) at the user's request to keep auditing past v0.7.63.0's theme fix. Most are honestly-scoped future work with no defect behind them; fixed the genuine ones: automation rules (`idleThresholdMinutes`, `jitterSeconds`, `interval`, `warningCountdownSecondsBeforeAction`) now reject invalid values with `400` instead of silently persisting and reinterpreting them later; `MystTiq-PalServer-Console.log` now rotates at 25 MB instead of growing forever on a long-running service; three stale "BACKEND REQUIRED" messages (Base ownership/recovery repair, the Setup checklist's UE4SS row, its Backup Storage row) corrected to match features that actually shipped in earlier versions. Newly-surfaced, not fixed this pass: `LinuxServerLifecycleService` has no PalServer console-capture at all, unlike Windows |
| **v0.7.65.0** | Shipped | Linux Console Capture Fix — direct follow-up request to fix the gap v0.7.64.0 flagged. Closer tracing found a more precise bug than "no capture at all": Linux's detached PalServer launch already redirected stdout/stderr via `setsid -f ... >> file 2>&1`, just into `ManagerRuntimeRoot/palserver-console.log`, a location nothing on the read side (Live Console, `HeadlessMonitoringService`, Doctor) ever looked at — all of those only check `LogsRoot/MystTiq-PalServer-Console.log`, the exact file Windows captures into. Relocated Linux's capture to that same filename/location, with rotation applied once per server start (the only point in this platform's detached-process architecture where rotation is safe, since bash holds one file handle open for the entire session). **Verified live against the real reference Linux VM** (192.168.1.143) via a fully isolated manual test (scratch directory, throwaway port, stand-in server script) that confirmed the correct log location, real captured stdout/stderr, and correct rotation behavior, without ever touching the live production service |
| **v0.7.66.0** | Shipped | Tray Reminder Toast Positioning Fix — direct bug report: on a multi-monitor layout (secondary monitor stacked above the primary at a negative Y origin), the "MystTiq is still running" tray toast rendered far from its intended bottom-right corner. Root cause: `PositionBottomRight()` resolved its target monitor via `Screens.ScreenFromWindow(this)` at the moment `Opened` fires, while the toast itself was still sitting at whatever default position the OS assigned a moment earlier. `TrayReminderToast` now accepts an optional owner `Window` and resolves the screen from it first; `App.ShowTrayStillRunningReminder` now passes `mainWindow`. A real, but incomplete, fix — see v0.7.67.0 |
| **v0.7.67.0** | Shipped | Tray Reminder Toast: Layout Timing Fix — completes v0.7.66.0 after actually testing it on the real reported hardware (this machine turned out to be that exact multi-monitor setup). Found a second, independent bug: `PositionBottomRight()` read `Bounds` before `SizeToContent` had measured the toast's real small content size — a non-zero but wrong placeholder (~1521×770) slipped past the existing `> 0` fallback guard, corrupting the bottom-right math. Fixed by also repositioning on `LayoutUpdated`. **Verified with an exact pixel match on the real hardware**: a standalone test harness drove the real class with a stand-in window on the actual secondary monitor — before the fix landed at `(766, -834)` against an expected `(1972, -127)`; after the fix, landed at exactly `(1972, -127)` |
| **v0.7.68.0** | Shipped | Graceful Shutdown: RCON-First Fix — diagnosed a real, previously-flagged-but-never-chased-down bug: graceful shutdown had been observed falling back to forced termination on effectively every real stop (v0.6.10.0's own session notes, "4/4 occurrences," never explained). Root cause: `Process.CloseMainWindow()` silently does nothing once MystTiq's own headless-mode window-hiding policy has hidden PalServer's window — `Process.MainWindowHandle` only resolves a handle for a currently-visible window. Fixed by trying Palworld's native RCON `Shutdown` command first (the game's own real graceful-exit path), purely additive to the existing fallback chain on both Windows and Linux. Verified with a real Source RCON protocol stub server proving the exact command reaches the wire correctly — not verified end-to-end against a real PalServer, disclosed explicitly |
| **v0.7.69.0** | Shipped | Stale Crash-State Fix (Windows) — another previously-flagged item from the same audit: a server shown "Crashed" on Windows only ever cleared via an explicit Start, not Stop. Root cause: `WindowsServerLifecycleService.StopAsync`'s "nothing to stop" branch never wrote to the persisted state store at all, unlike `LinuxServerLifecycleService`'s equivalent branch (already correct) — a genuine Windows-only inconsistency. Fixed to match Linux exactly. Verified with a real `WindowsServerLifecycleService` instance (fake process-inspector, pre-seeded Crashed state) proving `StopAsync` clears it correctly |
| **v0.7.70.0** | Shipped | Real Email Notification Dispatch — implemented Email as a real notification channel, previously a typed stub since v0.6.1.0 that just logged "not implemented." Sends via `System.Net.Mail.SmtpClient` (STARTTLS, optional auth), with the same retry-once shape Webhook/Discord already use. Verified with a real, minimal SMTP protocol stub server proving the exact envelope/auth/content reach the wire correctly — the stub's own first draft had a real protocol-shape bug (`SmtpClient` sends the AUTH LOGIN username inline, not as a separate line) caught and fixed during testing rather than shipped as a false pass |
| **v0.7.71.0** | Shipped | Console Source: PalDefender Log — direct follow-up to a user-provided screenshot of PalDefender Anti-Cheat startup diagnostics that never appeared in MystTiq's own Console page. Checked live against the real production install: PalDefender writes its own timestamped per-session log at `Pal\Binaries\Win64\PalDefender\Logs\{timestamp}.log`, confirmed to carry exactly the screenshot's content, including PalServer's own "Running Palworld dedicated server on :PORT" banner. `HeadlessMonitoringService.ResolveConsoleSources` never looked for it — added as a new merged source. Verified live against both the real production and clone server profiles. Does not add ongoing/live capture — that still needs the native DLL-proxy hook scoped (and deliberately not implemented, low disclosed payoff) in v0.7.57.0 |
| **v0.7.72.0** | Shipped | Console-Write Hook (Native Console Capture, completed) — completes the v0.7.57.0 foundation. Hooks `WriteConsoleA`/`WriteConsoleW` (not the originally-planned, non-exported `FOutputDevice::LogfImpl`) in PalServer-Win64-Shipping-Cmd.exe's own Import Address Table — the same path Unreal's console window writes through. Live-verified safe end-to-end on an isolated clone, twice (raw launch, then the real install-API → lifecycle-start → `/logs/tail` path) — never the real production server. New opt-in `HeadlessConsoleCaptureProxyService` + 3 routes, Install never automatic. Honest finding, confirmed empirically: the capture log never grows past its initial startup lines — Palworld's build genuinely writes nothing to console once steady-state running, so this adds real but modest new startup diagnostics, not ongoing gameplay console output. Installed on this machine's real `default`/`second-local` servers at direct user request, after live verification |
| **v0.7.73.0** | Shipped | Tray Icon Honestly Reflects Running State — direct live bug report: two real PalServer processes were running with no MystTiq tray icon visible. Investigated auto-start thoroughly (no Startup entry/Run key/scheduled task/service/automation rule anywhere; tab restore only reconnects, never starts) and found the real cause: "Exit GUI Only" always fully quit the app — taking the tray icon with it — even while deliberately leaving PalServer running. `ExitGui_OnClick` now checks `Tabs.Any(t => t.ServerIsRunning)` (same check the window's own close button uses) and collapses to minimize-to-tray instead of exiting when anything's running; only a genuinely idle app still exits with no tray. Tray tooltip now names what's running, refreshed every 5s, instead of a static string |
| **v0.7.74.0** | Shipped | Close-Dialog Exit Shortcuts & Tab-Restore Exception Isolation — two direct live reports after clicking through v0.7.73.0. (1) The close-confirm dialog only offered Cancel/Minimize; added Safe Exit/Force Exit buttons directly, sharing the same shutdown logic the tray menu already used (now pulled into public `App.SafeExitAsync`/`ForceExitAsync`). (2) Only 1 of 3 saved tabs reopened after a relaunch — saved data was intact, but the startup sequence ran local-dashboard init and tab restoration as one unguarded continuation, so a failure in the first silently killed the second before it ran; each phase now isolated in its own try/catch. A separately reported per-tab color bug was checked and found already fixed by other work already in the source tree — not re-implemented, credited honestly rather than claimed |
| **v0.7.75.0** | Shipped | Delete Player Completely / Copy Player — two new destructive player-save operations. Delete permanently removes a player's save file then cleans up their guild reference as a separate follow-up (reusing Remove Broken Member). Copy clones inventory/recipes/records/skins/quest progress onto another player while preserving the destination's own identity — real investigation first (decoded a real per-player .sav via the existing converter, the first time this codebase has touched that file type) established the exact copy/preserve field split. Found and fixed a real operation-lock-release-ordering bug during live testing on an isolated clone of the real production save; every result independently re-verified by decoding the raw save a second way outside the app. Also root-caused a separately reported "right-click looks broken" issue: Kick/Ban were always correctly wired, just correctly disabled with no visible disabled styling anywhere in the app — added a real `MenuItem:disabled` style. Base/Guild right-click convenience (same session, same request) deferred to its own follow-up |
| **v0.7.76.0** | Shipped | Base/Guild Right-Click Workflow — completes the deferred half. Bases: "Copy Base Info", "Transfer to Guild…" (populated guild dropdown), "Wipe Base Completely…". Guilds: "Transfer Leadership To…", "Add Player…", "Remove Broken Member…", "Claim Orphaned Guild…" (populated player dropdown for each). Every flow: Preview → real findings → mandatory safety-backup notice → confirm → Apply. No new server-side mutation code — reuses the already-shipped Base/Guild ownership backend entirely; only new Desktop orchestration (`SelectGuildDialog`, one reusable `ConfirmOperationDialog`, parallel ViewModel Preview/Apply methods that stay in sync with the existing in-page cards) |
| **v0.7.77.0** | Shipped | MOD/Workshop Detection Fixes, UE4SS Version via Hash, Per-MOD Repair — direct live investigation into 3 real reports. Fixed a Workshop-runtime detection bug (a Workshop item that IS the UE4SS runtime always showed "not installed" since the check looked in the mod inventory instead of checking UE4SS itself). UE4SS version now resolved via local-Workshop-copy SHA-256 hash match instead of "unavailable" — found and fixed a second bug along the way (two different UE4SS.dll files existed side by side, wrong one was being hashed). PalSchema/QualityOfLife properly imported at direct request — mods.txt now lists 8, matching what was remembered from v0.2.16.4; QualityOfLife's .pak files had been manually quarantined during a past investigation and were restored. New per-MOD Repair/Re-install (delete-then-reimport from a matched local Workshop source, reusing the existing delete snapshot as a safety net). Everything live-verified directly against the real production install |
| **v0.7.78.0** | Shipped | MOD Pages UX Pass — grew from direct live feedback on the MOD Library page's button clutter through several rounds of live iteration into a full pass across MOD Library, MOD Dashboard, and UE4SS. MOD Library: auto-loads local Steam Workshop MODs on first visit, ZIP install + Available Workshop Mods moved side by side at the top, Installed MODs (2/3) + MOD DETAILS (1/3) below, all-MODs actions moved to a new ribbon group, per-mod actions moved to a right-click menu, ZIP card simplified with real drag-and-drop, per-mod installed version + update-available badges added. MOD Dashboard: new color-coded hero health banner, recolored stat cards, and a deliberately lighter "MOD Overview" list to feel distinct from Library. UE4SS: fixed app-wide `Border.statuscard` missing padding, and Runtime Health now remembers a confirmed-good run instead of reverting to "Unverified" once its evidence disappears. Touches `MystTiq.Desktop` and `MystTiq.HeadlessHost` |
| **v0.7.79.0** | Shipped | Server Doctor Compact, Color-Coded Checks — direct live feedback: "these items listed should be compacted and make use of the spacing a bit more. Perhaps Pass can be highlighted in green or red." Each diagnostic finding is now a denser single row (colored state badge, component/category, evidence, actions) instead of a tall stacked card, with a redundant Recommendation-duplicates-Evidence line now hidden. PASS/WARNING/FAIL show as a small colored badge instead of plain text; a follow-up added a subtle text-shadow style to keep the badge label legible against the bright green PASS background. `MystTiq.Desktop` only |
| **v0.7.80.0** | Shipped | Server Setup Status Pill Centering & Glass Gradient — direct live feedback: "the Ready should be in the middle of the green. can we also add a gradient glassy effect." Fixed the per-row STATUS pill (Ready/Disabled/Missing) to explicitly stretch and center its label instead of relying on default layout behavior, and replaced its flat color fills with the app's existing glass-gradient family (a new `WarningGlassGradient` added for amber), matching the same glassy look already used on ribbon buttons elsewhere. `MystTiq.Desktop` only |
| **v0.7.81.0** | Shipped | "Set Up New Server" Wizard Rework — direct live feedback: "it should always be a local install... It should have the option to clone an existing server, setup a new world, import a world, and then move onto the specific settings." Rebuilt the wizard to actually distinguish "install a fresh server" from "connect to an existing one" (previously identical flows): always local, skips the old Local/Remote choice, walks through a new World Source step (Clone an Existing Server / Set Up a New World / Import a World), a read-only environment checklist during Install, and a starter preset picker (Vanilla/Balanced/Relaxed QoL) in Settings — all reusing existing backend capability, no new routes. New end-to-end smoke test (`Test-v0.7.81.0-RouteSmoke.ps1`) covers the whole flow live. `MystTiq.Desktop` only |
| **v0.7.82.0** | Shipped | Update Center Actionability & Server Setup Table Resize — direct live feedback: "the update center where it says update available should allow us to click on it to update. we have a couple that say unknown, we need to do better and have a way to check." Followed by: "the server environment should expand or shrink to fill the screen." pip's row gets a real "Update" button (`python -m pip install --upgrade pip` via a new mutation-gated route); the Unknown rows (Python/VC++ Runtime/MSVC Build Tools/PIM-Oodle) each get an "Open" link to their real official page since none has a reliable unattended version feed to automate. Server Setup's environment table now resizes with the window instead of a fixed 405px height. Also fixes a real Alert Center disk-space-prediction crash (took two passes to fully close), reorders Configuration to put Server Identity first with unsaved-change amber highlighting, fixes a Dashboard bug that could say "Stopped" while the ribbon only offered Stop, and fixes the Generate/Save As Preset buttons getting stuck disabled after the first Configuration load. Touches `MystTiq.Desktop` and `MystTiq.HeadlessHost` |
| **v0.7.83.0** | Shipped | New Server Wizard: Reordered Steps & Genuine Second-Server Support — direct live feedback: remove the standalone "Clone a Server" shortcut, reorder the wizard to World Source → Identity & Ports → World Settings → Install Directory → Download & Install, with the directory defaulting to the typical root unless already occupied. Every completion now registers a genuine, separate fleet profile (since "Local MystTiq" permanently owns the default identity) and automatically restarts the local MystTiq service to bring it online — no manual restart needed. Also fixes real bugs found live: a duplicate-connection guard blocking the wizard's own Connect, a silently-blank Step 1 header, Confirm showing a placeholder name instead of the real one, and a second round of the Alert Center crash fix. Touches `MystTiq.Desktop` (and `MystTiq.HeadlessHost` for the Alert Center fix) |
| **v0.7.84.0** | Shipped | Fleet Clone World Workflow — direct live feedback (built earlier, explicitly tabled at the time): "the clone world option in fleet should be a workflow with drop down options to clarify what it is doing... it should indicate better what server is being cloned." Clone World on the Fleet page is now a labeled workflow (SOURCE/NEW PROFILE/PORT OFFSET sections) instead of one flat card, plus a new "Restart MystTiq Now" button after a successful clone, reusing v0.7.83.0's `RestartOwnedSidecarAsync` to close the same "needs a restart" gap here. Touches `MystTiq.Desktop` only |
| **v0.7.85.0** | Shipped | Import a World, Phase 2 — closes the honest interim placeholder scoped out of v0.7.81.0. Verified live (not assumed) that PalServer.exe generates its own real world save within a few minutes of a genuinely fresh install's first-ever start, meaning the already-proven World Transactions import flow works unmodified once started once. New "Prepare for Import" step in the wizard's Install step (Import path only), World Settings choice skipped for Import, and a real bug fixed (`SelectedProfile`-dependent Analyze/Apply silently no-op'd during the wizard). Live end-to-end tested against a real server: started fresh, confirmed world generation, ran a real archive through Analyze → Apply successfully. Touches `MystTiq.Desktop` only |
| **v0.7.86.0** | Shipped | New Server Wizard: MODs Step — the last item scoped out of v0.7.81.0's original wizard rework. A new, optional MODs step (7 of 8) lets New World/Import installs scan and import Steam Workshop content inline before finishing setup, reusing the same commands already built for the MOD Library page verbatim (no new backend). Clone already carries over the source server's own MODs. Confirm & Finish renumbered to step 8 across the wizard. Touches `MystTiq.Desktop` only |
| **v0.7.87.0** | Shipped | Configuration: Advanced Dirty Highlighting, Save/Discard Prompt, Default Tab — direct live feedback: "When i change items on this page they should highlight... If I do not save the changes and click away it should ask if I want to save changes or discard. when i load up configuration it should always start with the servers settings." Advanced Settings rows now highlight when edited (matching Simple Settings' existing highlight), navigating away from Configuration with unsaved changes now prompts Save/Discard/Cancel, and Configuration always opens on Server Settings. Touches `MystTiq.Desktop` only |
| **v0.7.88.0** | Shipped | New Server Wizard and Dashboard Bug Fixes — direct live feedback with screenshots surfaced five real bugs: the wizard jumped straight to Confirm & Finish on every attempt when a default server was already running, the Install Directory step had no field to change the target path, Game Port needed highlighting and an explanatory note, the Clone source list was missing "Local MystTiq", and the Dashboard could get stuck showing "Starting / Not Ready" with Start greyed out after the server had genuinely stopped. All five root-caused and fixed. Touches `MystTiq.Desktop` only |
| **v0.7.89.0** | Shipped | Second-Server Lifecycle Fixes and Wizard Polish — direct live feedback while driving a genuine second local server through setup and Start surfaced a cluster of real bugs, most from code that treated "the literal default profile" as a stand-in for "a local server": Start silently failing with no message, a local server mislabeled "Remote", the Dashboard showing a different server's world data, a misleading "OVERALL PROGRESS" label, the Install step not auto-refreshing or showing progress detail, and the MODs step requiring a manual scan with no local-ZIP install option. All root-caused and fixed, with live verification against the real running server. Touches `MystTiq.Desktop` only |
| **v0.7.90.0** | Shipped | Install With Extras (UE4SS) — direct live feedback: "we should add that as part of the install, option to install just the server without mods and then the option of installing it with the extras." The wizard's Install step now offers "Install Palworld Dedicated Server" (server only, unchanged) alongside a new "Install with Extras (+ UE4SS)", which also auto-installs the newest stable UE4SS release via the same Preview/Apply flow the UE4SS page already uses. Palworld Save Tools and PIM/Oodle Decoder remain out of scope, confirmed neither has any install capability anywhere in the app yet. Touches `MystTiq.Desktop` only |
| **v0.7.91.0** | Shipped | Crash-Risk Configuration Detection — follow-up to a competitive feature review against other Palworld server managers: Doctor now flags `BuildObjectDeteriorationDamageRate = 0`, a community-reported cause of long-running server instability, which two of MystTiq's own bundled QoL presets set to exactly 0. Scoped narrowly to this one well-evidenced setting rather than also flagging vanilla-default-enabled settings like `bEnableInvaderEnemy`, which would produce false positives. Touches `MystTiq.HeadlessHost` only |
| **v0.7.92.0** | Shipped | World Map: Guild Base Markers and Click-to-Select — first of the "real gaps" from the competitive feature review. The live World Map plots guild bases from the decoded world save (verified live against the real world) in the experimental real-world-position mode, and clicking a player dot selects that player for Teleport To Me / Teleport To Player. Vanilla RCON has no teleport-to-coordinates command, so click-anywhere teleport is not offered. Touches `MystTiq.HeadlessHost` and `MystTiq.Desktop` |
| **v0.7.93.0** | Shipped | Nexus Mods Catalog — second of the "real gaps" from the competitive feature review. A collapsed card on the MOD Library page browses Trending / Latest Added / Latest Updated Palworld mods on Nexus Mods with the user's own API key (kept on their PC, never sent to the MystTiq server), looks a mod up by id or page URL, lists its files, and installs a ZIP through the existing validated install. Built around what Nexus actually allows: no search in the public API, and direct downloads only for Premium accounts (free accounts use Open on Nexus or paste an nxm:// link). Touches `MystTiq.Core` and `MystTiq.Desktop` |
| **v0.7.94.0** | Shipped | Starter Kits — third and last of the "real gaps" from the competitive feature review. A collapsed Starter Kits card on the Players page builds named item/Pal loadouts, gives one to a selected online player, or auto-gifts it to players MystTiq first sees after auto-gift is switched on (never existing players). Delivery uses PalDefender's give commands over RCON because vanilla Palworld has no give command; the card shows the real provider status and has a Test button. Touches `MystTiq.Core`, `MystTiq.HeadlessHost` and `MystTiq.Desktop` |
| **v0.8.14.0** | Shipped | Cards Follow the Signed-In Role — cards a signed-in role cannot use are hidden (read-only where it can only read), with a notice; Fleet Actions fixed to Operator as on the server. Checked for every role by the ArtworkHarness. |
| **v0.8.15.0** | Shipped | A Real Remote Sign-In — the Desktop signs in over pinned TLS to an isolated MystTiq on the Linux VM as Viewer, Operator and Admin (19/19: role, cards, server allow/refuse, sign-out); fixed a refused change being read as a success. |
| **v0.8.16.0** | Shipped | Theme Modes and Density — a per-tab Mode picker (Dark, Light, Midnight true black, High contrast, Follow the system) replaces the Light mode checkbox; Compact/Comfortable density for every tab. Checked by the ArtworkHarness (contrast per mode, system follow, density). |
| **v0.8.17.0** | Shipped | The HOST Tab, Process Priority and Eco Mode — the server machine's processor, memory, disks and network on a new HOST tab; per-server priority and eco mode (efficiency mode + below normal; automatic when no one is online), kept through restarts. Windows smoke on the real process, Linux VM, harnesses. |
| **v0.8.18.0** | Shipped | Bandwidth — per-server network limits (per player, updates per second) on the HOST tab, written into Engine.ini before every start; the game's real defaults shown; an upload estimate with a suggested limit. Windows smoke with supervisor restarts, the Linux start path on the VM, harnesses. |
| **v0.8.19.0** | Shipped | Every Route Declares Its Role — security fix: 105 routes had no role (RCON, kick/ban, settings, mods open to any signed-in role or other server's account); now Viewer to read and Admin to change by default, scoped; the Ribbon follows the role. Smoke with authentication on, harnesses. |
| **v0.8.20.0** | Shipped | Host History — the HOST tab charts processor, memory and upload over an hour, a day or a week (one reading a minute, kept 7 days, averages and peaks). Windows smoke, the Linux VM, harnesses. |
| **v0.8.21.0** | Shipped | Linux: Back to Full Speed After Eco Mode — the systemd unit lets the service raise the server's priority again (LimitNICE=-11, no capability; `service-unit` prints it; verified by systemd on the VM). Tests keep their own fleet folder (`--fleet-root`; 19 older smokes had used the real one). |
| **v0.8.22.0** | Shipped | Signing In, Tested From Linux Too — the window-control, settings and tab close icons are drawn (they were empty boxes on Linux and Windows 10); the remote sign-in test covers Owner, the Ribbon gating on every page and the server agreeing, and runs the Desktop on Linux too (38/38 on each). |
| **v0.8.23.0** | Shipped | Every Button Follows the Role — every command that needs more than Viewer (112, derived from the routes it calls) is disabled below its role, on every page, in menus and on the Ribbon; the gate checks the list against the code. Harness checks every role and page. |
| **v0.8.24.0** | Shipped | Processor Cores — pin a server to chosen cores on the HOST tab (Windows mask; Linux every thread), and priority, eco mode and cores applied as soon as a start through MystTiq succeeds. Windows smoke 6/6 on the real process, Linux VM every thread. No new crash evidence, so no new signatures. |
| **v0.8.25.0** | Shipped — accepted baseline | Every Colour Follows the Mode — the decorative colours outside the theme catalogue (92 colours, 21 shadows, the charts) follow every mode, Dark unchanged; Windows contrast themes used by High contrast and Follow the system; the HOST tab's own art and icon; accepted baseline |
| **v0.8.13.0** | Shipped | Game Names, and an Image on Every Ribbon Button — item and Pal names from the server's own game files (Give Item picker shows and searches names and lists the game's other items; the map shows species names), with an honest ids-only fallback; the user's last ten Ribbon images, so every button has one. Verified on Windows and the Linux VM. |
| **v0.8.12.0** | Shipped | Second-NAT Check, and UPnP That Works — the WAN check detects carrier-grade or double NAT from the router's UPnP WAN address (the route out only as weak evidence) and says plainly what cannot be proven from inside; UPnP fixed on Linux and on multi-interface Windows. Real network: no second NAT (an earlier draft's double-NAT warning was corrected). Verified on Windows and the Linux VM. |
| **v0.8.11.0** | Shipped | Pal Positions on the Map — owned Pals working at bases or in parties drawn on the Palpagos map at their last saved position, clustered with a count (a base's workers as a badge on it; click to zoom right in); Palbox Pals counted but not drawn, since their saved spot is stale. Verified with harness scenarios, a route smoke and the real world (11 on the map, 65 in a Palbox). |
| **v0.8.10.0** | Shipped | Ribbon Icon Originals — the user's full framed originals replace all 30 Ribbon images (nine batch 3 redesigns), resized to 512 px with hashes recorded; Quick Actions' Backup and Doctor now show the Create and Run Doctor images. Ten images are still needed for the remaining buttons (Refresh, Start, Restart, Stop, Console, Force Stop, Verify Files, Install Missing, Run Analysis, Preview Plan). |
| **v0.8.9.0** | Shipped | Crash Reports in the Crash Analyzer — Unreal crash reports (Pal\Saved\Crashes) are now analysed as timestamped evidence, with a new "invalid array size" engine signature taken from a real report; new reports with a new critical finding send an alert (old ones recorded silently; mute and the crash-alert switch respected). Verified with harness scenarios, a route smoke and copies of the server's three real crash reports. |
| **v0.8.8.0** | Shipped | Ribbon Image Icons — the user's three batches (30 full-colour illustrated Ribbon icons) across 13 pages, embedded unchanged with recorded hashes; image icons take precedence over vector icons and glyphs. Verified on every page that has them, in dark and light; only Run Analysis and Preview Plan keep a text glyph. |
| **v0.8.7.0** | Shipped | The Dashboard in Every Language — every fixed Dashboard label, button, tooltip and number template translated (German, Spanish) via the new TrFormat binding; status values still English. Fixed "MEMORY" splitting mid-word at 950x650 (English too); a new check fails on any split or cut-off Dashboard label in all three languages. |
| **v0.8.6.0** | Shipped | The Ribbon in Every Language — all 50 Ribbon labels and 26 group titles translated (German, Spanish), overflow menu included; icons still keyed by the unchanged English label. Fixed Ribbon buttons being cut off under the page header on UE4SS and Doctor (English too) by estimating button widths from their text. Verified on every page, in three languages, at two window sizes. |
| **v0.8.5.0** | Shipped | Display Language (first slice) — Settings → Appearance → Language (English, Deutsch, Español), applied live and remembered. Translated so far: category tabs, navigation, and every page title/subtitle; the rest stays English. English unchanged and the fallback for anything missing. Strings in `Assets/i18n/<code>.json`; the gate fails on any missing key. Verified with headless German/Spanish renders (live switch, tabs fit at 950x650) and the live app. |
| **v0.8.4.0** | Shipped | Pause Discord, Email & Webhooks — Alert Center can pause outside delivery for 1 h to 7 days (host clock, capped at 30 days) while every alert and notification still appears on the Notifications page; skipped sends are listed in the Activity log and never sent late. "Send Test Notification" checks the channels. New `GET /notifications/delivery`, `POST /notifications/delivery/pause`, `POST /notifications/test` (Admin). Verified with a real webhook receiver (harness + route smoke) and the live app. |
| **v0.8.3.0** | Shipped | Give Item Picker — a "Find an item or Pal" search under Player Administration listing the item and Pal ids that occur in the server's own world save (matched to the installed game version by construction), plus Starter Kit ids and ids given before; Add to Give / Add to Selected Kit writes the line (Pals by level). New read-only `GET /api/v1/players/give/catalog` (Operator), streamed and cached. Verified with 5 harness scenarios, a route smoke (19 MB save in 0.3 s), the real world read-only (122 items, 29 Pal species) and the live app. |
| **v0.8.2.0** | Shipped | Service Mode Fixes — first release on the accepted baseline v0.8.1.0. `service-run` (installed Windows service / systemd unit) now waits for the configured game port instead of always UDP 8211 (servers on other ports no longer "fail to start" and stop the service); the embedded API no longer runs a second crash-recovery loop for the service's own profile; service mode now sends crash/recovery/give-up alerts, persists its recovery state, and after an OS-triggered restart announces the server back up and unpins the DOWN notice. New end-to-end service-mode smoke with a stand-in PalServer on a custom port. |
| **v0.8.1.0** | Shipped | Ribbon Icons — the user's Palworld-inspired vector icons for the ten main Ribbon actions (Refresh, Start, Restart, Stop, Backup, Console, Doctor, Verify Files, Install Missing, Force Stop), drawn to the package spec in the existing theme colours; other "Refresh …" buttons and "Restart Server" follow for consistency, everything else keeps its glyph. Path data checked against the supplied package by the gate; 42 new rendering checks in dark and light; live app verified. |
| **v0.8.0.0** | Shipped | Artwork Refresh — first 0.8.x release. The user's updated graphics from the v0.7.110.1 artwork build, 3-way merged into the current code with zero conflicts: 26 distinct navigation icons, day/night artwork for all seven categories (header, workspace, setup wizard), and a header with its own layout column so small windows no longer let it cover the Ribbon or category tabs. Everything from v0.7.111.0–v0.7.115.0 kept. Checked with the artwork build's 149 offline rendering checks against the merged code, the full gate and live screenshots. Closes deficiency item 6. |
| **v0.7.115.0** | Shipped | Deficiency Fixes — items 1–5 of the deficiency report supplied after the 0.7.x pass, each confirmed against the code and app first. Map labels laid out as real text boxes beside their markers (and player names, which had never rendered, now show); crash recovery's restart window, give-up and pinned DOWN notice survive a MystTiq restart; "back up" waits for the game port, not just a process; operation history reloads with cut-off operations marked Interrupted; the checklist no longer names an old version and the full gate no longer fails on its own build folders or crashes without a baseline archive. Item 6 (icons/header art) deferred. |
| **v0.7.114.0** | Shipped | Multi-User Login — last of the four items pulled into 0.7.x on 2026-09-22. Named accounts (username, display name, role, optional single server, password) created by the Owner on the Security page and signed in from Settings; they ride the existing auth middleware and role checks, so a role change applies at once and disabling or resetting signs a person out everywhere. Salted PBKDF2 passwords, hashed 12-hour sessions, account lockout after 5 failures, and the audit log now names who made each call. Checked with 3 new logic-harness scenarios and an 11-check live smoke with authentication really on. |
| **v0.7.113.0** | Shipped | Teleport Points — third of the four items pulled into 0.7.x on 2026-09-22. Players teleport from anywhere to a named point by typing a chat command (`!tp spawn`), set up on the Map page's Teleport Points card (cooldown, capture an online player's position, send a player now). A chat command because Palworld's own fast travel cannot be opened up from the server. MystTiq reads PalDefender's chat and runs its `tp` over RCON, acting only for a player online under exactly that name and id (the route smoke caught and fixed an impostor path before shipping). Checked with 5 new logic-harness scenarios and a live chat-to-RCON smoke; not yet checked with a real player. |
| **v0.7.112.0** | Shipped | Give Item — second of the four items pulled into 0.7.x on 2026-09-22. The Players page's Give Item is real: a "Give items or Pals" box on Player Administration (Starter Kits' line format) and a live right-click entry, delivered through PalDefender over RCON like kits, only to an online player, with a plain reason when PalDefender or RCON is missing and refusal of any id that is not a plain Palworld id. Not a kit claim. Checked with a new logic-harness scenario and a live route smoke of every refusal; an item arriving in a real player's inventory is not yet verified. |
| **v0.7.111.0** | Shipped | Alert Mute and Crash Alert Settings — first of the four items pulled into 0.7.x on 2026-09-22. The Alert Center page now has per-profile Crash Alerts settings (switch crash-recovery alerts off, or drop the "server is back up" notices) and a Mute Alerts card (1 hour, 8 hours, 24 hours or 7 days, always ending by itself, capped at 30 days). Crash recovery keeps restarting a muted server; skipped crash alerts go to the Activity log; anything still wrong alerts when the mute ends. Checked with 3 new logic-harness scenarios, a live smoke with a real crash while muted, and in the Desktop. |
| **v0.7.110.0** | Shipped | Crash Recovery: Watching After a Give-Up — the last real gap named this session: when automatic crash recovery gave up, it pinned a "server is DOWN" notice with no way to ever unpin itself, unlike the Alert Center's own alerts (v0.7.104.0). MystTiq now keeps watching after a give-up (api-run/Desktop sidecar only; the OS-service `service-run` mode's own exit-on-give-up contract is untouched) and, the moment the server is running again — started by you, not the loop — unpins the notice, sends a "back up" notice, and genuinely resumes crash-detect-and-restart monitoring. Checked with two logic-harness scenarios and a live smoke against the real Windows lifecycle service, including a real process the OS recognizes as PalServer. A GitHub release for 0.7.x and contacting Nexus Mods are now v0.8.x roadmap items. |
| **v0.7.109.0** | Shipped | Map: Spreading Out Exactly-Coincident Markers — the one residual gap v0.7.106.0's own doc named: two markers at the exact same screen position still rendered as a single icon, since that version only ever staggered labels, never the dots. A genuinely coincident cluster now fans out around the shared point (first marker undisturbed, others spread a few pixels away); anything with real separation is untouched, using a much tighter radius than the label-collision fix. Checked with a new logic-harness scenario and live on the real Map page, where a cluster of 4 exactly-coincident offline players — previously a single ring — now shows as 4 distinct rings. |
| **v0.7.108.0** | Shipped | Alert Center: Configurable Reminder Interval — v0.7.107.0's reminder cadence was an environment-variable-only live-testing knob; it's now a real "Reminder every (minutes, 0 = off)" field on the Alert Center page, saved by the existing Save Rules button, applying to every rule. Defaults to 1440 (24 hours); a rules file saved before this version loads with that same default. The environment variable still overrides it for live testing. Checked with a new logic-harness scenario (including loading an old rules file) and a live smoke that deliberately proves the setting itself drives the cadence. |
| **v0.7.107.0** | Shipped | Alert Center: Reminder Re-alerts — named as a follow-up since v0.7.102.0 first introduced episode-based alerting: a condition that stayed true for days sent exactly one alert and then nothing else until it cleared. A condition still true after 24 hours now gets a "Still active: ..." reminder, repeated every 24 hours until it clears; the reminder is never pinned (only the original alert is, and it still unpins itself on recovery), so a long-running condition cannot grow the pinned count without bound. Checked with two new logic-harness scenarios and a live smoke with the interval shortened for testing. |
| **v0.7.106.0** | Shipped | Map: De-overlapping Marker Labels — named as a follow-up since v0.7.100.0's map-zoom work: players or bases standing close together drew their name labels directly on top of each other. Labels whose text would collide now stagger into a readable column, and the fix reacts to zoom, un-staggering once markers spread apart on screen. The dot itself is never moved, only its label. Checked live on the clone's real Map page, where a base and a nearby offline player's overlapping labels now draw cleanly. |
| **v0.7.105.0** | Shipped | Doctor: Confirm Before Fix — the Doctor's Fix button (Create Nightly Backup Rule, Create Backup Folder, Install / Repair Server Files) ran the instant it was clicked; a confirmation step was named as a desired follow-up since v0.7.103.0. Every Fix button now needs a "Confirmed" checkbox ticked first, with a tooltip naming exactly what the fix does, the same pattern Backup Restore already uses. Checked with a new logic-harness scenario and on the running app. |
| **v0.7.104.0** | Shipped | Alert Center: A Pinned Alert Unpins Itself — a pinned Critical alert (Low disk space by default) used to stay pinned forever, even once its own "Resolved: ..." notice had already arrived, a gap named in v0.7.102.0. It now unpins itself the moment the condition clears, and unpins itself too if the rule is switched off mid-episode (which sends no Resolved notice, by design, so nothing else names that case). Nothing else about alerting changed. Checked live on two isolated sidecars and on the final published binary. |
| **v0.7.103.0** | Shipped | Doctor: Scheduled Backups and a One-Click Fix — the Doctor said whether a backup existed but not whether anything would make the next one happen (the Default Server has no automation rules and a 12-day-old backup). New finding warns when no rule creates backups, they are all off, none runs at least weekly, or the scheduled rule last failed; with no backup rule at all it offers "Create Nightly Backup Rule" (daily 03:00 UTC), which is Admin-only, idempotent under simultaneous clicks, and never duplicates a rule. Doctor fix buttons are now labelled by what they do. Nothing is created until pressed. |
| **v0.7.102.0** | Shipped | Deficiency Fixes From Walking the Running App — seven defects found by driving the real app, all fixed: a Configuration phantom "unsaved change" that would have overwritten a real setting on Save (Item Corruption Rate 0.05 under a 0.1 slider minimum), Alert Center alerts that repeated forever (now one alert per episode plus a recovery notice, remembered across restarts), the Doctor and Alert Center disagreeing about disk space (now one shared rule), a stopped server shown as "Network: ERROR", a clipped retention box, "Up to date" hiding that the install is newer than the published release, and no hint that nothing is scheduled. Checked live on a real sidecar and on the running app. |
| **v0.7.101.0** | Shipped | Crash Alerts: Tell the Admin When the Server Crashes — the crash-recovery loop restarted a crashed server but only wrote to the console. It now sends notifications for a crash (with the likely cause and first fix from the logs), a recovery, a failed restart, and giving up (pinned, server is down), through the normal notification routes. Verified end to end on a real sidecar with a seeded crash; delivery to Discord/email/webhooks and a real Palworld crash not verified. |
| **v0.7.100.0** | Shipped | Map: Zoom, Click-to-Zoom, and Last-Known Player Positions — scroll-wheel zoom toward the cursor, drag to pan, click a marker or name to zoom in on it, and offline players drawn at the last position the save recorded (hollow rings) alongside online players at their live position (solid dots), with online and offline marked in the lists. Real-world last positions all fall within a few map units of the start plateau, confirming the map calibration. Checked live on the running app; solid online dots not seen (nobody online). |
| **v0.7.99.0** | Shipped | Dedicated Map Page Under World — added because the map was a card on the Players page and could not be found. Map is first in the page with players, bases and in-game coordinates beside it. Looking at the running app for the first time (window capture plus synthetic clicks) found and fixed two long-standing bugs: every map marker was drawn in the top-left corner, and markers were centred on their label rather than their point. Player dots not seen (no player online). |
| **v0.7.98.0** | Shipped | Doctor: Disk, Backups, Admin Password, Memory and Recent Crashes — fourth of the "enhancements to features we already have". The Doctor checked paths and ports but not the things that end a server's life. It now checks disk space on every platform (it was Linux-only) including whether the next backup fits, backup freshness judged against the world's last change, admin access (empty, very common or short password with REST or RCON on, never echoed), memory, and unreviewed critical Crash Analyzer findings. These feed the Dashboard badge. Found a real 6.4-day backup gap on the Default Server while testing. Page rendering and Linux paths not verified. |
| **v0.7.97.0** | Shipped | Crash Analyzer: Known Causes, Fixes, and No More Repeats — third of the "enhancements to features we already have". The headless analyzer was a keyword counter (its out-of-memory needle matched "room" and "zoom", and every UE4SS mention was a warning). Findings now come from a 12-signature catalog with a plain-language cause and fixes, MystTiq's own exit-code lines are translated, installed mods named in the evidence are listed as a lead, findings are marked new versus already reported, and the page shows the selected finding's detail. Page rendering and real-crash fix text not verified. |
| **v0.7.96.0** | Shipped | Live Map: Players and Bases Work Out of the Box — second of the "enhancements to features we already have", prompted by maps being supplied but no player or base positions being visible. The World Map is open by default and starts on the Palpagos map, real positions and guild bases are on by default, and the world-to-image conversion is calibrated to the expanded map (it assumed the pre-expansion frame) using the documented start location and a real world's base and trail. Accurate to a few pixels; card rendering not verified without GUI automation. |
| **v0.7.95.0** | Shipped | Discord Bot: Live Status, Join/Leave Feed, Autocomplete, More Commands — first of the "enhancements to features we already have". Reading the bot first showed per-role permission mapping already existed, so the real gaps were filled instead: a self-updating status message in a chosen channel, bot presence, an optional join/leave/server-state events feed, kick/ban autocomplete from online players, new `/mysttiq-save`, `/mysttiq-backup` and `/mysttiq-unban`, and a safety fix so replies can no longer ping the guild through a player name. Not verified against real Discord (no bot token available). Touches `MystTiq.Core`, `MystTiq.HeadlessHost` and `MystTiq.Desktop` |
| **v0.8.x** | Planned | Adaptive application/server/network efficiency, priorities, eco modes and host/per-server dashboards; plus give-item/inventory editing (contingent on a companion-mod dependency decision), multi-user web login with per-person roles, and multi-language UI, added per the 2026-09-17 competitive feature review sequencing |
| **v1.0** | Goal | Stable production release |

The full ten-milestone `v0.6.x.0` breakdown, with scope and acceptance detail for each, lives in [`docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md`](docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md); that ten-milestone family (v0.6.0.0–v0.6.9.0) shipped complete on 2026-09-03. `v0.6.10.0` is a direct user-requested addition beyond that original plan (Clone World plus extended live verification) — see [`docs/architecture/v0.6.10.0-clone-world-live-verification.md`](docs/architecture/v0.6.10.0-clone-world-live-verification.md). Historical release implementation detail belongs in [`CHANGELOG.md`](CHANGELOG.md), [`release-notes/`](release-notes/), and [`docs/history/`](docs/history/) rather than being duplicated here.

## Documentation

The documentation layout is intentionally separated by purpose:

- [`README.md`](README.md) — current product overview
- [`CHANGELOG.md`](CHANGELOG.md) — complete release history
- [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) — active release/promotion checklist
- [`docs/README.md`](docs/README.md) — documentation index
- [`docs/architecture/`](docs/architecture/) — current architecture/audit documents
- [`docs/linux/`](docs/linux/) — Linux reference environment and headless implementation notes
- [`docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md`](docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md) — the authoritative, testable v0.6.x roadmap
- [`docs/roadmap/PRODUCT_ROADMAP.md`](docs/roadmap/PRODUCT_ROADMAP.md) — forward-looking product direction
- [`docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md`](docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md) — improvements discovered during Linux work for shared/core or Windows backport
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
