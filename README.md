<!-- MystTiq v0.8.26.0: file reviewed for this release (2026-09-27). -->
<p align="center"><img src="docs/images/github-banner-v0.8.25.0.png" alt="MystTiq Palworld Server Manager"></p>

# MystTiq Palworld Server Manager

An independent, open-source administration app for Palworld dedicated servers on Windows and Linux. Manage local servers or connect to a remote MystTiq service from the Avalonia desktop.

**Current version: v0.8.26.0 · Accepted baseline: v0.8.25.0 · Next milestone: v0.9.0.0 — translation completion · Target: v1.0 stable**

[Downloads](https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases) · [Release notes](release-notes/v0.8.26.0.md) · [Roadmap](docs/roadmap/PRODUCT_ROADMAP.md) · [Report an issue](https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/issues) · [Contributing](CONTRIBUTING.md)

## Current status

v0.8.26.0 is the current version; v0.8.25.0 is the accepted development baseline. Neither is the v1.0 stability milestone. Availability of downloadable builds is shown on the Releases page. Windows and Linux share the desktop and headless service; full Linux desktop acceptance remains open.

v0.8.26.0 is a clean-up and verification release with no change to how the app behaves. The legacy WPF app and its installer are gone, along with about a thousand obsolete files; every remaining file was reviewed for this version. New scripts run the checks that used to need a person: a real Linux desktop session (window, clipboard, tray), in-game Give Item and teleport on a test server, real alert delivery, and Windows contrast themes. v0.8.25.0 made decorative colours follow every mode, respected Windows contrast themes and gave HOST its own artwork.

![MystTiq dashboard](docs/images/01-dashboard.png)

*The screenshot illustrates the interface; see the current release notes for changes.*

## What it does

- **Operate servers:** setup and adoption, start/stop/restart, updates, configuration, scheduling, crash recovery and diagnostics.
- **Protect worlds:** backups and retention, restore checks, world/player/guild inspection, and guarded repair and migration workflows.
- **Manage a community:** player tools, a world map, starter kits, give-item/Pal tools, teleport points, whitelist, temporary bans and Discord integration. Some actions require supported server mods.
- **Manage mods:** inventory, enable/disable, health checks, rollback, UE4SS/Workshop support and Nexus integration. Nexus download options depend on the user's account and Nexus policies.
- **Tune the host:** CPU, memory, disk and network information; seven-day history; per-server priority, eco mode, processor cores and game-level bandwidth settings.
- **Share administration:** named accounts, Viewer/Operator/Admin/Owner roles, scoped API permissions, audit records and remote connections with pinned TLS.
- **Choose your appearance:** themed artwork, Dark/Light/Midnight/High contrast/Follow system modes, density settings and partial German/Spanish translation with English fallback.

The headless service owns server operations and persistent state. The desktop is its client: closing the window does not stop a managed server.

## Download and run

1. Open [Releases](https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases) and read the notes and known limitations for the selected version.
2. For Windows x64, download the **Windows-x64.zip** asset, extract the whole folder, and launch **MystTiq.Desktop.exe**. Keep the `headless` folder beside it. This self-contained package does not require a separate .NET installation.
3. Connect to or configure your MystTiq service in the app. Keep existing server data and backups separate from the extracted application folder.

The **FullSource.zip** is for development. GitHub's automatic source archives are also source, not runnable downloads. Linux source builds are available; do not infer desktop acceptance from a successful build alone.

## Build from source

Requires the **.NET 10 SDK** and **PowerShell 7** for the scripts. Run these commands from the repository root:

```powershell
dotnet build src/MystTiq.HeadlessHost/MystTiq.HeadlessHost.csproj -c Release
dotnet build src/MystTiq.Desktop/MystTiq.Desktop.csproj -c Release

# Build a self-contained Windows desktop and matching service package:
pwsh ./scripts/Package-GitHubRelease.ps1 -Runtime win-x64
```

Use `-Runtime linux-x64` to build a Linux ZIP; after extraction on Linux, grant execute permission to `MystTiq.Desktop` and `headless/mysttiq-server`. Windows native console capture additionally requires the MSVC x64 toolchain and `scripts/Build-ConsoleProxy.ps1`; the release workflow builds and includes that helper.

The application is `src/MystTiq.Desktop` plus `src/MystTiq.HeadlessHost` and `src/MystTiq.Core`. The legacy WPF app (`src/PalworldManager`) and its installer were removed in v0.8.26.0; they remain in the git history and earlier source archives.

## Roadmap to v1.0

| Milestone | Status | Scope |
| --- | --- | --- |
| v0.8.25.0 | Accepted baseline | Theme completion, HOST artwork, role enforcement, host controls/history and service fixes |
| v0.8.26.0 | Current | Legacy WPF app removed, repository clean-up, every file reviewed, live acceptance scripts, Windows and Linux packages |
| v0.9.0.0 | Next | Finish page content, status values, dialogs, messages and accessible names in the existing translation system; review German and Spanish |
| v0.9.x | Planned stabilization | Live integration checks, Linux desktop/service acceptance, accessibility, upgrade/recovery and release packaging verification |
| v1.0 | Target, no date committed | Release only after the documented acceptance gates pass and remaining limitations are published |

See the [full roadmap](docs/roadmap/PRODUCT_ROADMAP.md) for open verification work, dependencies and optional ideas. Completed changes belong in the [changelog](CHANGELOG.md) and [release notes](release-notes/), not the future-work list.

## Known limitations before v1.0

- Translation coverage is incomplete. More languages and CJK font coverage need separate review.
- On a real Linux desktop the window, maximize, clipboard and tray are checked automatically (v0.8.26.0); file pickers and priority restoration through an installed service still need acceptance testing.
- Actual item/Pal delivery, live player-map behaviour and PalDefender teleport coordinate calibration need an online test player: `scripts/Test-v0.8.26.0-InGame.ps1` runs these once someone joins a test server. Real Discord/email delivery is checked by `scripts/Test-v0.8.26.0-Alerts.ps1`, which sends real test messages.
- Disabled controls do not yet consistently explain which role is required. Linux desktop contrast-theme integration remains open.
- OS-level traffic shaping, per-process network accounting, a browser UI and per-accent artwork are not promised for v1.0.

## Documentation and support

- [Product roadmap and v1.0 gates](docs/roadmap/PRODUCT_ROADMAP.md)
- [Publishing a release](docs/release/README.md)
- [Release acceptance checklist](RELEASE_CHECKLIST.md)
- [Linux testing notes](docs/linux/TESTED_ENVIRONMENT.md)
- [Security reporting](SECURITY.md) · [Contribution guide](CONTRIBUTING.md)

## License and attribution

MystTiq code is provided under the [MIT License](LICENSE). Palworld names and third-party/game artwork remain the property of their respective owners; the code license does not grant rights to those assets. MystTiq is an independent community project and is not affiliated with Pocketpair.
