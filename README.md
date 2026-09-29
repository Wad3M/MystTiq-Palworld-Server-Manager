<!-- MystTiq v0.9.6.0: file reviewed for this release (2026-09-29). -->
<p align="center">
  <img src="docs/images/github-banner-v0.8.25.0.png" alt="MystTiq — Palworld Server Manager" width="100%">
</p>

<h1 align="center">Your worlds. Your servers. One place to manage them.</h1>

<p align="center">An open-source desktop for running Palworld communities.<br>Manage servers, protect worlds, organize mods, and share administration across your team.</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-67e8d5" alt="Code license: MIT"></a>
  <img src="https://img.shields.io/badge/stage-pre--1.0-b9a4ff" alt="Development stage: pre-1.0">
  <img src="https://img.shields.io/badge/desktop-Windows%20%7C%20Linux-80caff" alt="Desktop targets: Windows and Linux">
  <img src="https://img.shields.io/badge/language%20options-12-67e8d5" alt="12 language options; non-English translations await review">
</p>

<p align="center">
  <a href="https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases"><strong>Browse downloads</strong></a> ·
  <a href="#get-started">Get started</a> ·
  <a href="docs/roadmap/PRODUCT_ROADMAP.md">Roadmap</a> ·
  <a href="https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/issues/new/choose">Get help / contribute</a>
</p>

**Current version: v0.9.6.0 · Accepted baseline: v0.8.25.0**

This source snapshot is a development release on the way to v1.0. Check the Releases page for published downloads; the version shown here does not imply a binary has been published. Windows is the current release-workflow download target. Linux builds are available from source, with desktop acceptance still in progress.

## What's new in v0.9.6.0

**Let players in, and find your servers in seconds.** Settings, Diagnostics and the new-server wizard now show whether Windows Firewall lets players reach the port your server really uses, with an **Allow through Firewall** button that asks Windows for administrator rights when needed. The rule follows the port: change it, allow again, and the old port is closed. The check ignores rules that only look like they help (the game client's, Store apps', the launcher's), and a second server set up through the wizard now gets its own port.

The server search checks 256 addresses at a time, shows the ranges it searches and its progress, lists each server as it answers, and can be cancelled; virtual adapters are searched only when you ask, and you can add ranges of your own. v0.9.5.0 made update checks truthful and stopped servers from starting each other; earlier v0.9 releases brought 12 languages and accessible names. The 11 non-English translations are drafts awaiting native-speaker review.

[Release notes](release-notes/v0.9.6.0.md) · [Translation inventory](docs/i18n/UI_TEXT_INVENTORY.md) · [Help review a translation](CONTRIBUTING.md#translation-feedback)

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

Nexus download options depend on the account and provider. Game-level bandwidth settings are not OS-level traffic shaping. See [known limitations](#known-limitations) for features awaiting live verification.

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

- **Translation:** the desktop's labels and messages, the service's messages and the game's item/Pal names are available in all 12 languages; the 11 translations need native review. English fallback remains available.
- **Windows x64:** the release workflow packages the desktop, matching service and native console helper.
- **Linux x64:** shared desktop/service source and local packaging are available. XFCE window, clipboard and tray checks are recorded in the roadmap; file pickers, scaling, other desktops and installed-service priority recovery remain open. Chinese/Japanese/Korean require suitable system fonts such as Noto Sans CJK.

## Roadmap to v1.0

| Milestone | Status | Focus |
| --- | --- | --- |
| v0.9.0.0 | Delivered | Labels, menus and language selection in 12 languages |
| v0.9.1.0 | Delivered | Desktop status, progress, errors and dialogs follow the chosen language |
| v0.9.2.0 | Delivered | Game-provided item and Pal names in the chosen language; composed map and tooltip text |
| v0.9.3.0 | Delivered | The service's own messages follow the chosen language; review sheets for native reviewers |
| v0.9.4.0 | Delivered | Disabled controls name the role they need; accessible names and Tab reach throughout |
| v0.9.5.0 | Delivered | Servers no longer start each other; true update checks (Steam's public build, PalDefender); upgrade, restore and rollback tested |
| v0.9.6.0 | Current | Firewall rule for each server's own port where the port is set; a second server gets its own port; server search in seconds, showing its ranges |
| v0.9.7.0 | Next | Avalonia 12: the desktop moves to the current UI framework, checked page by page in every appearance mode |
| v0.9.x | Planned | Native translation review, live integration evidence, a screen-reader pass and Linux acceptance |
| v1.0 | Target | Stable release after the acceptance gates pass; no date committed |

The [full roadmap](docs/roadmap/PRODUCT_ROADMAP.md) separates delivered work, remaining checks and optional ideas. Release history lives in the [changelog](CHANGELOG.md).

## Known limitations

- Non-English catalogs are draft translations awaiting native review. Text the service passes on from Windows, Linux or the game (error details, log lines) is shown as it arrives, usually in English, and dates and numbers use one fixed format.
- Item/Pal delivery, live map/teleport calibration and real Discord/email delivery still need live acceptance with a player and configured channels. Test scripts exist; that alone is not evidence of a passed live test.
- Linux acceptance remains partial. Accessible names and keyboard reach are checked automatically; a pass with a real screen reader and Linux native contrast integration still need work.
- A browser UI, per-accent artwork, OS-level traffic shaping and per-process network accounting are optional ideas, not v1.0 commitments.

## Build and contribute

Install the **.NET 10 SDK** and **PowerShell 7**, then run from the repository root:

```powershell
dotnet build PalworldServerManager.slnx -c Release
pwsh ./scripts/Package-GitHubRelease.ps1 -Runtime win-x64
```

For Linux packaging use `-Runtime linux-x64`; after extracting, grant execute permission to `MystTiq.Desktop` and `headless/mysttiq-server`. Windows native console capture additionally requires MSVC x64 and `scripts/Build-ConsoleProxy.ps1`; the release workflow includes this step.

The app consists of `MystTiq.Desktop`, `MystTiq.HeadlessHost` and `MystTiq.Core`. The former Windows app and installer were removed in v0.8.26.0 and remain in Git history.

[Contribution guide](CONTRIBUTING.md) · [Publishing guide](docs/release/README.md) · [Release checklist](RELEASE_CHECKLIST.md) · [Linux testing](docs/linux/TESTED_ENVIRONMENT.md) · [Security reporting](SECURITY.md)

## License and attribution

MystTiq code is [MIT licensed](LICENSE). Palworld names and third-party/game artwork belong to their respective owners; the code license does not grant rights to those assets. MystTiq is an independent community project and is not affiliated with Pocketpair.
