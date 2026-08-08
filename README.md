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

MystTiq Palworld Server Manager is a free and open-source Windows application designed to simplify hosting, monitoring, maintaining, and repairing **Palworld Dedicated Servers**.

Instead of juggling configuration files, command-line tools, backups, mods, and save editors, MystTiq brings everything together into a single modern desktop application built for both first-time server owners and experienced administrators.

## Highlights

| Feature | Description |
|---|---|
| Modern Dashboard | Live server health, CPU, RAM, and activity monitoring |
| World Explorer | Inspect players, guilds, bases, and world data |
| Player Management | Browse connected players and server information |
| Guild and Base Tools | Inspect guild ownership and base information |
| Backup Center | One-click backups and restore workflows |
| Workshop and UE4SS | Built-in mod management and validation |
| World Validator | Detect issues before they become problems |
| Open Source | MIT licensed and community driven |

## Dashboard

<p align="center">
  <img src="docs/images/01-dashboard.png" alt="MystTiq dashboard" width="100%">
</p>

## Why MystTiq?

> **Managing a dedicated server should be easy.**

Whether you are hosting a family server for a few friends or managing a larger community, MystTiq provides the tools needed to monitor, maintain, and troubleshoot your server through a clean and intuitive Windows interface.

- No subscriptions
- No telemetry
- No unnecessary complexity
- MIT licensed
- Built for self-hosters

## Features

### Server Administration

- Start, stop, and restart
- Live status monitoring
- CPU and RAM history
- Health monitoring
- Notification center
- Activity timeline
- Update management

### World Management

- World Explorer
- Player Inspector
- Guild Manager
- Base Inspector
- Save data inspection

### Backup and Recovery

- One-click backups
- Restore workflow
- Backup validation
- Backup history

### Mod Platform

- Steam Workshop integration
- UE4SS support
- Runtime validation
- Crossplay verification
- Installed mod inventory

### Validation and Repair

- World validation
- Repair planning
- Safe maintenance workflow
- Future Transaction Engine support

## Screenshot Gallery

### Server Settings

<p align="center">
  <img src="docs/images/02-settings.png" alt="Server settings" width="100%">
</p>

### Player Manager

<p align="center">
  <img src="docs/images/03-players.png" alt="Player manager" width="100%">
</p>

### Guild Manager

<p align="center">
  <img src="docs/images/04-guilds.png" alt="Guild manager" width="100%">
</p>

### World Explorer

<p align="center">
  <img src="docs/images/05-world-explorer.png" alt="World Explorer" width="100%">
</p>

### Backup Manager

<p align="center">
  <img src="docs/images/06-backups.png" alt="Backup manager" width="100%">
</p>

### Mod Management

<p align="center">
  <img src="docs/images/07-mods.png" alt="Mod management" width="100%">
</p>

### Notifications

<p align="center">
  <img src="docs/images/08-notifications.png" alt="Notifications" width="100%">
</p>

## Feature Matrix

| Feature | Included |
|---|:---:|
| Live Dashboard | Yes |
| Resource Monitoring | Yes |
| Server Health | Yes |
| World Explorer | Yes |
| Player Inspector | Yes |
| Guild Manager | Yes |
| Base Inspector | Yes |
| Backup Manager | Yes |
| Restore Workflow | Yes |
| Steam Workshop | Yes |
| UE4SS Support | Yes |
| World Validation | Yes |
| Portable Version | Yes |
| Open Source | Yes |
| MIT License | Yes |

## Quick Start

1. Download the latest release.
2. Extract the portable ZIP.
3. Launch **MystTiq.exe**.
4. Select your Palworld Dedicated Server installation.
5. Start managing your server.

## Requirements

- Windows 10 or Windows 11 (64-bit)
- Palworld Dedicated Server
- Administrator privileges may be required for:
  - Windows services
  - Firewall configuration
  - Protected installation folders

## Download

The latest builds are available from the GitHub **Releases** page.

Available packages include:

- Portable Windows ZIP
- Windows Installer *(planned)*
- Source code

## Building From Source

Clone the repository:

```bash
git clone https://github.com/Wad3M/MystTiq-Palworld-Server-Manager.git
```

Open:

```text
PalworldServerManager.slnx
```

Build:

```text
Release | x64
```

Or use PowerShell:

```powershell
./scripts/Build.ps1
```

## Documentation

Documentation will live under the `docs` folder.

Planned guides include:

- Installation
- Configuration
- Backup and restore
- World Explorer
- Mod management
- Troubleshooting
- FAQ

## Roadmap

| Version | Status | Highlights |
|---|:---:|---|
| **v0.2.14.11** | Current | Public release and repository polish |
| **v0.2.15** | Planned | Documentation and packaging improvements |
| **v0.3.0** | Planned | Transaction Engine, Repair Queue, Preview Engine, and Rollback Framework |
| **v1.0** | Goal | Stable production release |

## Contributing

Contributions, bug reports, feature requests, and documentation improvements are always welcome.

Please read:

- [CONTRIBUTING.md](CONTRIBUTING.md)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

before opening issues or submitting pull requests.

## Security

If you discover a security issue or accidentally expose credentials, please follow [SECURITY.md](SECURITY.md).

## License

Released under the **MIT License**.

See [LICENSE](LICENSE) for complete details.

## Disclaimer

MystTiq Palworld Server Manager is an independent community project.

It is **not affiliated with, endorsed by, or sponsored by Pocketpair, Inc.**

Palworld and all related trademarks are the property of their respective owners.

## About MystTiq

MystTiq is an open-source initiative focused on building modern, approachable management tools for self-hosted game servers.

The long-term vision is to provide a consistent management experience across multiple dedicated server platforms while remaining completely free and open source.

---

<p align="center">
  <strong>If MystTiq has been helpful, consider starring the repository.</strong>
</p>

<p align="center">
  Built for the self-hosting community.
</p>
