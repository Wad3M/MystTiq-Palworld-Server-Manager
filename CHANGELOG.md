# Changelog

All notable public changes will be documented here.

## [0.2.14.1] - 2026-08-05

### Added
- Dedicated Workspace Manager page.
- Portable and installed deployment-mode visibility.
- Workspace path inspection and validation.
- Folder-opening and path-browsing controls.
- Synchronized server, SteamCMD, and backup path saving.
- Notification diagnostics and self-test controls.
- Automatic startup initialization for Players, Guilds, Bases, and recovery data.

### Changed
- Workspace Manager buttons now follow compact MystTiq button standards.
- Workspace cards and path rows use reduced spacing.
- Configuration and World Editing layouts now display more controls on screen.
- Player-save discovery now requires valid Palworld player-save filenames and stable files.
- Backup Center now offers a coordinated stop, backup, and restart workflow when Palworld locks an active save file.
- Startup world-data stages now run independently so one unavailable subsystem does not prevent other data from loading.

### Fixed
- Dashboard Guild and Base totals no longer remain at zero until a manual refresh.
- Invalid, temporary, malformed, recovery, and zero-byte player files are no longer displayed as players.
- Previously imported invalid unknown-player records are removed during startup.
- Running-server backups no longer fail after repeated locked-file copy attempts.
- Notification self-test entries can be cleared without removing genuine notifications.
- Workspace Manager no longer uses oversized non-standard buttons.
- Fixed a startup `NullReferenceException` caused by constructor-time logging initialization.
- Fixed a startup XAML resource error caused by missing Workspace button style definitions.


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
