# MystTiq Palworld Server Manager

> **Professional Windows management software for Palworld Dedicated Servers.**

MystTiq Palworld Server Manager is a free and open-source Windows application designed to simplify hosting and maintaining Palworld dedicated servers. It provides a modern dashboard for monitoring server health, managing worlds, creating backups, inspecting save data, and maintaining server installations—all from a single desktop application.

---

## Features

### 🖥️ Server Management

- Start, Stop, and Restart your server
- Server update management
- Live operational status
- Overall server health monitoring
- Real-time CPU and RAM history
- Activity timeline
- Notification center

### 🌍 World Management

- World Explorer
- Player Inspector
- Guild Manager
- Base Inspector
- Save inspection utilities

### 💾 Backup & Recovery

- One-click backups
- Restore workflow
- Backup history
- Backup validation

### 🧩 Mod Management

- Steam Workshop integration
- UE4SS support
- Installed MOD inventory
- Runtime validation
- Crossplay verification

### 🎨 Modern User Interface

- Responsive one-page dashboard
- Professional dark MystTiq theme
- Standardized buttons and tooltips
- Live activity feed
- Consistent status indicators

---

# Screenshots

> Screenshots will be added as the project continues to evolve.

---

# Downloads

Download the latest version from the **Releases** section of this repository.

Available packages include:

- Portable Windows x64 ZIP
- Windows Installer
- Source Code

---

# Requirements

- Windows 10 or Windows 11 (64-bit)
- Palworld Dedicated Server
- Administrator privileges may be required for:
  - Windows Services
  - Firewall configuration
  - Protected installation folders

---

# Building From Source

## Prerequisites

- Visual Studio 2022
- .NET 10 SDK
- .NET Desktop Development workload

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

Create a portable package:

```powershell
./scripts/Package-Portable.ps1
```

---

# Safety

Working directly with Palworld save data always carries some risk.

Before performing repairs or maintenance:

- Stop the server
- Create a backup
- Verify the selected world
- Test restore procedures

Never publish:

- Server passwords
- REST credentials
- Steam credentials
- Player save files
- Backup archives
- Sensitive log files

---

# Roadmap

## Current Release

**v0.2.13.2**

## Planned

### v0.2.13
- Portable packaging improvements
- Windows installer
- Release automation

### v0.2.14
- Documentation expansion
- GitHub Pages website
- User guides

### v0.3.0
- Transaction Engine
- Repair Queue
- Preview Engine
- Rollback Framework

---

# Contributing

Contributions are welcome!

Please read:

- CONTRIBUTING.md
- CODE_OF_CONDUCT.md

before submitting issues or pull requests.

---

# Security

If you discover a security issue or accidentally expose credentials, please follow the instructions in:

**SECURITY.md**

---

# License

This project is licensed under the **MIT License**.

See **LICENSE** for details.

---

# Disclaimer

MystTiq Palworld Server Manager is an independent community project.

It is **not affiliated with, endorsed by, or sponsored by Pocketpair, Inc.**

Palworld and all related trademarks are the property of their respective owners.

---

# About MystTiq

MystTiq is an open-source project focused on building modern, easy-to-use server management tools for self-hosted game servers.

The long-term vision is to provide a consistent management experience across multiple dedicated server platforms while remaining completely free and open source.
