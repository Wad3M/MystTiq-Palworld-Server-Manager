# Changelog

All notable public changes will be documented here.

## [0.2.14.9 FIX3] - 2026-08-06

### Changed
- Update Center row actions now use compact sizing that fits the action column.
- Update Center actions now use semantic colors for update, verify, source, install, retry/check, enable/disable, manage, and create operations.

### Fixed
- Admin Commands runtime status now recognizes common package-name and successful-load message variants.
- Admin Commands status now updates from both process output and tailed `Pal.log` content, including when MystTiq adopts an already-running server.

### v0.2.14.9 FIX1
- Added required Windows installer generation and Inno Setup bootstrap tooling.
- Installer assets now publish to `artifacts` with SHA-256 checksums.
- UE4SS runtime compatibility now refreshes from the live MOD inventory after every MOD state change.
- Removed stale hard-coded MOD compatibility guidance.

## [0.2.14.9 FIX2] - 2026-08-06

### Fixed
- Mouse-wheel routing across nested grids, lists, logs, and page-level scroll viewers.
- Diagnostics Center now scrolls correctly when the pointer is over the results grid.
- Parent pages automatically receive wheel input when a nested control reaches its scroll boundary.

## [0.2.14.9] - 2026-08-06

### Added
- Release-candidate validation for repository hygiene, XAML resources, event handlers, version consistency, dialog usage, and release documentation.
- Unified `Build.ps1 Validate` action.

### Changed
- Full release preparation now validates the source tree before build and packaging.

## [0.2.14.8] - 2026-08-06

### Added
- Central application constants for non-user-configurable timing and network defaults.
- Dedicated MainWindow lifecycle partial for startup, timer ownership, cancellation, and shutdown cleanup.
- Window-lifetime cancellation token to stop startup work cleanly when the application closes.

### Changed
- MainWindow constructor no longer contains the complete loaded/closed orchestration logic.
- Monitor, automation, and heartbeat timer subscriptions now have named handlers and are explicitly detached at shutdown.
- Startup and monitor exceptions are isolated, logged, and surfaced through the notification infrastructure without crashing the UI thread.
- Long-lived cancellation sources and disposable services are released through one predictable shutdown path.
- Repeated operational timing values now consume shared constants to reduce configuration drift.

### Safety
- No world, player, guild, base, backup, MOD, or configuration-editing behavior was intentionally changed.
- This release is focused on lifecycle reliability, maintainability, and shutdown safety.

## [0.2.14.5] - 2026-08-06

### Added
- Guided Repair Center status banner, world scan action, repair candidate summary cards, and selection controls.
- Read-only repair-plan workflow with candidate, selected, and high-risk counts.

### Changed
- World Inspector is now the single navigation entry point for world validation and repair workflows.
- Removed the redundant World Validator item from the left navigation.
- Repair Center uses shared MystTiq dark-theme cards, semantic buttons, tooltips, and compact spacing.

## [0.2.14.3] - 2026-08-06

### Added
- Central application dialog facade and expanded dialog service.
- Shared MystTiq button density variants for standard, compact, toolbar, wide, icon, and DataGrid actions.
- Formal UI standards documentation under `release-notes`.

### Changed
- Existing application dialogs now route through one central integration point while preserving current behavior.
- Historical apply notes, build/test plans, and compile hotfix notes now live under `release-notes`.
- Environment DataGrid actions now use the shared DataGrid button standard.

## [0.2.14.2] - 2026-08-05

### Added
- Central player-save discovery and validation service.
- Guarded filesystem enumeration service.
- Ordered startup coordinator with independent stage results.

### Changed
- Players, Guilds, World discovery, World Tools, and Player Recovery now share the same player-save rules.
- Volatile Palworld filesystem scans now tolerate files and folders changing during enumeration.


## [0.2.14.7] - 2026-08-06

### Added
- Diagnostics Center under Tools with read-only application, workspace, server, world, backup, MOD, transaction, and notification checks.
- Provider-based `DiagnosticsService` architecture for registering subsystem health checks.
- Weighted health score with passed, warning, and failed totals.
- JSON and text diagnostics report export.
- Redacted support-package ZIP containing diagnostics, selected configuration metadata, and recent size-limited logs.

### Changed
- Diagnostics and support artifacts are stored in the centralized diagnostics directory for installed and portable modes.
- Transaction Center read-only wording no longer embeds an obsolete version number.


## [0.2.14.6] - 2026-08-06

### Added
- Read-only Transaction Center inside World Inspector.
- Durable transaction and world-import journal discovery.
- Search, filtering, stage details, backup/report links, and rollback-availability reporting.

### Changed
- Transaction History foundation is now an active audit center.

### Safety
- Rollback execution remains disabled until the rollback framework is validated.
- Malformed transaction records are skipped and logged without interrupting history loading.

## [0.2.14.4] - 2026-08-06

### Added
- Dark-themed World Management migration wizard inside World Inspector.
- Progressive seven-step migration workflow with locked, active, and completed states.
- World Inspector tabs for Players, Guilds, Bases, World Validator, World Management, Repair Center, and Transaction History.
- Guided status banner explaining the current migration state and next required action.
- Sidebar World Validator shortcut now opens the validator inside World Inspector.

### Changed
- World Validator is now organized as a World Inspector tab rather than a separate working page.
- Existing repair preview is presented as Repair Center.
- Player, Guild, and Base tabs link to their full management pages while preserving one World Inspector workspace.
- Locked migration stages explain what must be completed before they become available.

## [0.2.14.1] - 2026-08-05

### Added
- Dedicated Workspace Manager page.
- Portable/installed mode visibility and path inspection.
- Workspace validation, folder-opening, browsing, and synchronized path saving.


## [0.2.13.2] - 2026-08-05

### Added
- Portable workspace and application path foundation.
- Automatic PalServer and SteamCMD discovery inside the portable workspace.
- Portable-local settings, logs, cache, notifications, diagnostics, and window state.

### Changed
- Portable packages now include a working directory layout and marker file.
- Installer builds now use non-portable published files.

## [0.2.13.1] - 2026-08-04

### Added
- Central version definition in `Directory.Build.props`.
- Runtime application-version service.
- Shared version reader for build and packaging scripts.
- Release-tag version validation.

### Changed
- Window title, sidebar, user agents, exports, packaging, and installer builds now consume the central version.

## [0.2.12] - 2026-08-04

### Added
- First public open-source baseline.
- Professional one-page operations dashboard.
- Compact CPU and RAM history graphs.
- Live activity ticker and notification center.
- Separate operational-state and overall-health reporting.
- Health breakdown tooltip.
- Standardized MystTiq dark-theme buttons and tooltips.
- Rounded transparent application icon.

### Fixed
- Intentionally stopped servers no longer report poor health solely because they are stopped.
- Live-save temporary files no longer interrupt player and world inspection.
- Notification bell now toggles correctly.
- Clearing the final notification closes the flyout and hides the bell immediately.

## [0.2.14.1 FIX1]
### Fixed
- Backup Center now handles Palworld-exclusive live save locks with an optional coordinated stop-backup-start workflow instead of failing after repeated copy attempts.

## v0.2.14.1 FIX2

### Fixed
- Dashboard guild/base totals now initialize on first launch.
- Players, Guilds, Bases, and guild/base recovery snapshots are preloaded automatically after startup.
- Startup world-data stages fail independently so one unavailable subsystem does not prevent the rest of the application from loading.


## v0.2.14.11
- Finalized Update Center polish
- Admin Commands refresh reliability improvements
- Scroll routing audit
- Documentation and release wrap-up completed
