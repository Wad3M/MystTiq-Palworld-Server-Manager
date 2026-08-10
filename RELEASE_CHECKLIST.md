# MystTiq Release Checklist

This is the active release checklist for MystTiq Palworld Server Manager. Historical version-specific acceptance criteria are archived under [`docs/history/`](docs/history/).

## Current release state

- **Official validated baseline:** v0.2.16.3 FIX2
- **Current release candidate:** v0.2.16.4 — SteamCMD Distribution Abstraction & Final Windows Platform Audit
- **Supported application platform:** Windows 10/11 x64
- **Linux support:** planned for v0.3; not released in v0.2.16.x

## Source and version

- [ ] `Directory.Build.props` contains the current application version.
- [ ] `src/PalworldManager/app.manifest` is synchronized.
- [ ] `README.md` and `docs/index.html` identify the current release candidate accurately.
- [ ] Only the current release logic harness remains active in `scripts/`.
- [ ] `SOURCE_MANIFEST_SHA256.txt` is regenerated after final changes.
- [ ] No stale current-series version references remain in active source/build scripts.

## Required build sequence

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.4-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

- [ ] Clean succeeds.
- [ ] Validate succeeds with 0 errors and 0 warnings.
- [ ] All succeeds.
- [ ] v0.2.16.4 logic harness passes with zero failures.

## Release assets

- [ ] Windows x64 portable ZIP is produced.
- [ ] Windows installer is produced.
- [ ] `SHA256SUMS.txt` is generated.
- [ ] Release asset checksums verify successfully.
- [ ] Changed-files ZIP and complete-source ZIP are retained for development handoff.

## Runtime regression

- [ ] Start / Stop / Restart / Force Stop work.
- [ ] Running-server adoption works.
- [ ] CPU/RAM/session monitoring follows the active PalServer session.
- [ ] Backup and restore workflows remain functional.
- [ ] World Inspector live-save snapshot handling works while PalServer is saving.
- [ ] WORLD PULSE remains accurate and informational.
- [ ] MOD Library inventory/state is correct.
- [ ] Native UE4SS module evidence remains functional.
- [ ] Disabled and Active / Unverified MODs remain neutral to Overall Health.
- [ ] Confirmed MOD failures/errors reduce health appropriately.
- [ ] Start Without MODs remains functional.

## v0.2.16.4 distribution/platform acceptance

- [ ] Existing SteamCMD installation is detected.
- [ ] SteamCMD install/repair and self-update work.
- [ ] Palworld Dedicated Server install with validation works.
- [ ] Fallback install without validation works when required.
- [ ] Default Steam library recovery still works when SteamCMD ignores `force_install_dir`.
- [ ] Server Update works while the server is stopped.
- [ ] Update cancellation terminates the SteamCMD process tree.
- [ ] SteamCMD package/App-ID/argument policy is owned by `IServerDistributionPlatformService`.
- [ ] No Linux support is implied or advertised as released.

## UI

- [ ] MystTiq dark-theme standards are preserved.
- [ ] Button semantic colors remain correct.
- [ ] Buttons remain compact, readable, and grid-aligned.
- [ ] Tooltips are present and useful on non-obvious controls.
- [ ] Common window/DPI sizes show no clipping or overflow.
- [ ] Server Setup status badges remain compact and aligned.

## Documentation and GitHub

- [ ] README describes the current product rather than duplicating release history.
- [ ] CHANGELOG contains the complete release history.
- [ ] Version-specific release notes remain under `release-notes/`.
- [ ] Historical architecture/design notes remain under `docs/history/`.
- [ ] Current architecture documentation remains under `docs/architecture/`.
- [ ] Public GitHub Pages documentation reflects the supported Windows status and planned Linux direction.
- [ ] Release description is prepared.
- [ ] Portable ZIP, installer, and checksums are attached to the GitHub release.

## Promotion gate

Promote v0.2.16.4 only after the build, logic harness, runtime regression, distribution tests, documentation review, and release assets are all accepted.
