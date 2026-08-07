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

- Windows 10 or newer
- PowerShell 7 recommended; Windows PowerShell 5.1 is supported
- .NET 10 SDK
- Visual Studio 2022 with the .NET Desktop Development workload (optional)
- Inno Setup 6 or 7 for installer generation

Clone the repository:

```bash
git clone https://github.com/Wad3M/MystTiq-Palworld-Server-Manager.git
```

The root build entry point is `Build.ps1`:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

`All` runs validation, the Release build, portable packaging, installer generation, checksum generation, and checksum verification. Inno Setup is discovered from an explicit `-ISCC` path, `INNO_SETUP_HOME`, `PATH`, standard Inno Setup 6/7 install folders, or Windows registry entries.

Useful actions:

```powershell
.\Build.ps1 Build
.\Build.ps1 Package
.\Build.ps1 Installer
.\Build.ps1 Checksums
.\Build.ps1 Release
.\Build.ps1 All -SkipInstaller
.\Build.ps1 InstallerTools
```

Release assets are written to `artifacts/`. `SHA256SUMS.txt` covers the generated portable ZIP and Windows installer.

Open `PalworldServerManager.slnx` and use `Release | x64` when building in Visual Studio.

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

# Project Status

**Project Status:** Active Development

**Current Stable Release**  
**v0.2.14.11 — Update Center & UI Reliability**

**Official Baseline**  
v0.2.14.11

**Current Development Branch**  
v0.2.15.0

---

# Roadmap

## Current Stable Release

### v0.2.14.11 — Update Center & UI Reliability

**Completed**

### Build & Release Modernization
- Modernized Build.ps1
- Added Build-Release.ps1
- Added Build-Checksums.ps1
- Modernized Build-Installer.ps1
- Automatic Inno Setup 6/7 detection
- SHA-256 checksum generation and verification
- Improved build validation
- Improved release automation

### Update Center & UI Reliability
- Polished Update Center interface
- Standardized semantic button colors
- Improved Admin Commands refresh reliability
- Scroll routing improvements
- Updated validation logic
- Documentation synchronization

---

## Next Planned Release

### v0.2.15.0 — World Management Expansion

**Planned**

- Expanded World Validator
- World Repair Center
- World Import & Migration
- Character Repair tools
- Guild repair improvements
- World validation enhancements
- Additional save inspection improvements

---

## Future Releases

### v0.3.x — Transaction & Recovery Framework

**Planned**

- Transaction Engine
- Repair Queue
- Preview Engine
- Rollback Framework
- Advanced world recovery
- Expanded repair diagnostics

---

## Long-Term Vision

The long-term goal is to provide a modern, reliable, and free management platform for self-hosted Palworld dedicated servers while continuing to expand world management, repair automation, backup safety, and overall usability.

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


## Release Status

Current stable release: **v0.2.14.11 – Update Center & UI Reliability**.

This release finalizes the v0.2.14.x series.
