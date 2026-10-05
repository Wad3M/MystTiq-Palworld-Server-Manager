<!-- MystTiq v1.0.0.2: file reviewed for this release (2026-10-05). -->
<p align="center">
  <img src="docs/images/github-banner-v0.8.25.0.png" alt="MystTiq — Palworld Server Manager" width="100%">
</p>

<h1 align="center">Your worlds. Your servers. One place to manage them.</h1>

<p align="center">An open-source desktop for running Palworld communities.<br>Manage servers, protect worlds, organize mods, and share administration across your team.</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-67e8d5" alt="Code license: MIT"></a>
  <img src="https://img.shields.io/badge/release-1.0-b9a4ff" alt="Release: 1.0">
  <img src="https://img.shields.io/badge/desktop-Windows%20%7C%20Linux-80caff" alt="Desktop targets: Windows and Linux">
  <img src="https://img.shields.io/badge/language%20options-12-67e8d5" alt="12 language options">
</p>

<p align="center">
  <a href="https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases"><strong>Browse downloads</strong></a> ·
  <a href="#get-started">Get started</a> ·
  <a href="docs/roadmap/PRODUCT_ROADMAP.md">Roadmap</a> ·
  <a href="https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/issues/new/choose">Get help / contribute</a>
</p>

**Current version: v1.0.0.2 · Accepted baseline: v1.0.0.0**

MystTiq 1.0 is the first stable release. Downloads are on the Releases page; Windows is the packaged download, and Linux builds are available from source.

## What's new in v1.0.0.2

**Unique player names.** Each player name now belongs to one account, with upper and lower case counted as the same name. A player who joins with someone else's name is turned away, and you can reserve or block names on the Players page.

**Also new since 1.0 (v1.0.0.1): launcher, players and a cleaner exit.** A new **Server > Launcher** page with one-click troubleshooting presets, and servers that start just like a manual start. MystTiq now protects each Steam player's character when they join, helps when a start gets stuck (test without MODs, or find the MOD), lists the server's local and public addresses on the Dashboard, and shows Steam IDs on the Players page. Closing the window sends MystTiq to the tray; **Exit** stops everything.

[Release notes](release-notes/v1.0.0.2.md) · [Translation inventory](docs/i18n/UI_TEXT_INVENTORY.md) · [Help review a translation](CONTRIBUTING.md#translation-feedback)

## See your server at a glance

![MystTiq dashboard with server controls, health and world information](docs/images/01-dashboard.png)

*Repository screenshots illustrate the interface and may show an earlier build.*

## Built around everyday administration

| Work you need to do | Tools in MystTiq |
| --- | --- |
| **Run your servers** | Setup and adoption, start/stop/restart, updates, configuration, scheduling, diagnostics and crash recovery |
| **Protect your worlds** | Backups, retention and restore checks, plus guarded world/player/guild repair and migration workflows |
| **Look after your community** | Player tools, world map, kits, item/Pal tools, teleport points, whitelist, temporary bans and Discord integration; some actions require a supported mod/provider |
| **Keep mods organized** | Inventory, enable/disable, health checks, rollback, UE4SS/Workshop support and Nexus integration |
| **Understand the host** | CPU, memory, disk and network information, seven-day history, priority, eco mode, processor cores and game-level bandwidth settings |
| **Share administration** | Named accounts, Viewer/Operator/Admin/Owner roles, scoped permissions, audit records and remote connections with pinned TLS; disabled controls name the role they need |
| **Make it comfortable** | Dark, Light, Midnight, High contrast and Follow system modes, density settings, themed artwork and 12 language options |

Nexus download options depend on the account and provider. Game-level bandwidth settings are not OS-level traffic shaping.

<details>
<summary><strong>More of the interface: backups, players and mods</strong></summary>

### Backups

![Backup management](docs/images/06-backups.png)

### Players

![Player administration](docs/images/03-players.png)

### Mods

![Mod management](docs/images/07-mods.png)

</details>

## Get started

1. Open [Releases](https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases) and read the selected version's notes.
2. Download its **Windows-x64.zip**, extract the entire folder, and run **MystTiq.Desktop.exe**. Keep the `headless` folder alongside it. The package includes .NET.
3. Configure a local service or connect to a remote MystTiq service. Keep existing server data and backups separate from the application folder.

| Download | Who it's for |
| --- | --- |
| `Windows-x64.zip` | Windows users who want to run the app |
| `FullSource.zip` / GitHub source archives | Developers who want to build or inspect the source |
| `SHA256SUMS.txt` | Checking that downloaded ZIPs match the release |

Upgrading? Back up your server data, extract the new app into a fresh folder and follow that release's upgrade notes. Keep your existing configuration/data locations and verify the connection before resuming administration.

**Closing the desktop does not stop a managed server.** The headless service owns operations and persistent state; the desktop connects to it locally or remotely.

## Languages and platform status

English · 简体中文 · Español · Português (Brasil) · Русский · Deutsch · Français · 日本語 · 한국어 · Italiano · Polski · Türkçe

- **Translation:** the desktop's labels and messages, the service's messages and the game's item/Pal names are available in all 12 languages, with English always available. Suggestions from native speakers are welcome.
- **Windows x64:** the release packages the desktop, matching service and native console helper.
- **Linux x64:** the desktop and service build from the same source. Chinese/Japanese/Korean need suitable system fonts such as Noto Sans CJK.

## Release history

| Release | Status | Focus |
| --- | --- | --- |
| v0.9.0.0 – v0.9.4.0 | Delivered | 12 display languages for labels, messages and game names; roles explained; accessible names and keyboard reach |
| v0.9.5.0 – v0.9.8.0 | Delivered | Servers never start each other; true update checks; firewall per server port; fast server search; Avalonia 12; alerts when a component falls behind; Linux install |
| v0.9.9.0 – v0.9.10.0 | Delivered | One local helper; running servers survive adding one; crash causes for joins; UE4SS compared automatically; names kept in every language |
| v1.0.0.2 | Current | Unique player names: each name belongs to one account, case ignored |
| v1.0.0.1 | Previous | Server > Launcher; characters protected on join; help for stuck starts; addresses on the Dashboard; a clean Exit from the tray |
| v1.0.0.0 | Stable baseline | The first stable release |
| v1.1 | Planned | A MOD browser connected to Nexus Mods and other online repositories |

What comes next is in the [roadmap](docs/roadmap/PRODUCT_ROADMAP.md). Release history lives in the [changelog](CHANGELOG.md).

## Build and contribute

Install the **.NET 10 SDK** and **PowerShell 7**, then run from the repository root:

```powershell
dotnet build PalworldServerManager.slnx -c Release
pwsh ./scripts/Package-GitHubRelease.ps1 -Runtime win-x64
```

For Linux packaging use `-Runtime linux-x64`; after extracting, grant execute permission to `MystTiq.Desktop` and `headless/mysttiq-server`. Windows native console capture additionally requires MSVC x64 and `scripts/Build-ConsoleProxy.ps1`; the release workflow includes this step.

The app consists of `MystTiq.Desktop`, `MystTiq.HeadlessHost` and `MystTiq.Core`. The former Windows app and installer were removed in v0.8.26.0 and remain in Git history.

[Contribution guide](CONTRIBUTING.md) · [Publishing guide](docs/release/README.md) · [Release checklist](RELEASE_CHECKLIST.md) · [Linux testing](docs/linux/TESTED_ENVIRONMENT.md) · [Security reporting](SECURITY.md) · [Code signing policy](CODE_SIGNING_POLICY.md) · [Privacy policy](PRIVACY.md)

## License and attribution

MystTiq code is [MIT licensed](LICENSE). Palworld names and third-party/game artwork belong to their respective owners; the code license does not grant rights to those assets. MystTiq is an independent community project and is not affiliated with Pocketpair.
