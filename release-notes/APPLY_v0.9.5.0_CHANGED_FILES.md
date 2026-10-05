<!-- MystTiq v1.0.0.6: file reviewed for this release (2026-10-05). -->
# v0.9.5.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.5.0.
- `src/MystTiq.Core/Services/WindowsServerSessionInspector.cs`: process paths read with `QueryFullProcessImageName`
  (limited query access), `MainModule` only as a fallback.
- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs`, `LinuxServerLifecycleService.cs`: a process whose path
  cannot be read belongs to a server only when that server started it.
- `src/MystTiq.Core/Services/ServerGamePort.cs` (new), `src/MystTiq.HeadlessHost/Program.cs`: readiness waits for the
  `-port=` launch argument (8211 without one), PublicPort only as a fallback.
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs`: the Palworld server compared with Steam's public build
  from SteamCMD (cached 15 minutes); a PalDefender row; `PeekPublicBuild`, `InstalledServerBuild`,
  `PalDefenderGameWarning`. The Steam Web API's `UpToDateCheck` is gone.
- `src/MystTiq.HeadlessHost/HeadlessServerDistributionService.cs`: a refused manifest is retried by checking every file
  against the new build; `SteamCmdFailure` names SteamCMD's reason.
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs`, `LocalManagementApiHost.cs`: Doctor findings for the game
  server's build and PalDefender's warning.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: six scenarios (unreadable process, own process, expected port,
  SteamCMD app info, SteamCMD failures, PalDefender's warning).
- `scripts/Test-v0.7.110.0-RouteSmoke.ps1`, `scripts/Test-v0.7.115.0-RouteSmoke.ps1`: their stand-in servers bound the ini's
  PublicPort by hand; they are now launched with a matching `-port=`, as the real game binds (found by this gate).
- `scripts/Test-v0.9.5.0-Upgrade.ps1`, `scripts/Test-v0.9.5.0-FleetRecovery.ps1` (new smokes);
  `scripts/Test-v0.9.5.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.5.0.
- Docs: `docs/architecture/v0.9.5.0-recovery-and-updates.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` and its index, `docs/release/README.md`,
  `docs/release/GITHUB_PRESENTATION.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`, the issue
  templates.
