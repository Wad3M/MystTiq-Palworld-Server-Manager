<!-- MystTiq v1.0.0.5: file reviewed for this release (2026-10-05). -->
# Release checklist

**Current version: v1.0.0.5. Accepted baseline: v1.0.0.0.** Read the [publishing guide](docs/release/README.md) for commands and asset names. Historical version-specific checks are preserved in [history](docs/history/RELEASE_CHECKLIST_pre_v0.8.25.0.md); the [roadmap](docs/roadmap/PRODUCT_ROADMAP.md) defines the additional v1.0 gates.

## Source and documentation

- [ ] `Directory.Build.props`, application version, release tag and release notes agree.
- [ ] README, website and active roadmap distinguish shipped work, planned work and unverified integrations.
- [ ] No obsolete source files break a clean checkout; upstream fixes/dependency updates are preserved.
- [ ] No saves, credentials, local configuration, logs, compiled objects, build output or stale manifests are staged in Git.
- [ ] Asset attribution and distribution permissions have been reviewed.

## Build and verification

- [ ] Release builds of `MystTiq.Desktop` and `MystTiq.HeadlessHost` pass on the claimed platforms.
- [ ] Current-version logic checks and release validation pass; environment-dependent skips are recorded.
- [ ] Windows package includes `MystTiq.Desktop.exe`, matching `headless/mysttiq-server.exe` and the native console helper.
- [ ] Clean-machine extraction/startup and an isolated upgrade preserve settings, server data and backups.
- [ ] Start/stop/restart, readiness, backup/restore and recovery work with disposable test data.
- [ ] Roles, remote sign-in, refusal paths, theme modes, keyboard access and supported layouts are checked.
- [ ] Live-player, external notification and Linux graphical/service checks are completed or clearly reported as limitations; no skipped check is described as passed. The live scripts are `scripts/Test-v0.8.26.0-InGame.ps1`, `-Alerts.ps1`, `-ContrastTheme.ps1` and `-LinuxDesktopSession.ps1`.

## Publication

- [ ] CI passes on the reviewed commit before tagging; an existing release tag is never moved.
- [ ] Windows ZIP, FullSource ZIP and SHA256SUMS.txt correspond to the reviewed source and version.
- [ ] Release notes identify the executable, upgrade guidance, prerequisites and known limitations.
- [ ] Draft release assets and target commit are reviewed before publishing (a full release for v1 and later, not a prerelease).
- [ ] Verify the published download links and checksums after publication.

Use isolated working directories. Packaging must not stop live servers or clean unrelated paths.
