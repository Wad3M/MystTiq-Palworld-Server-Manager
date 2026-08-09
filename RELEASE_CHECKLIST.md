# MystTiq Release Checklist

## Source and version
- [ ] `Directory.Build.props`, assembly metadata, installer metadata, README, and active documentation contain the intended version.
- [ ] Release notes, build test plan, and apply instructions exist under `release-notes/`.
- [ ] `.\Build.ps1 Validate` passes; use `-StrictValidation` for final release auditing.
- [ ] No `bin`, `obj`, `.vs`, `artifacts`, logs, saves, backups, credentials, signing keys, or private data are included.

## Required build sequence
- [ ] `.\Build.ps1 Clean` succeeds.
- [ ] `.\Build.ps1 Validate` succeeds.
- [ ] `.\Build.ps1 All` succeeds.
- [ ] If installer tooling is intentionally unavailable, `.\Build.ps1 All -SkipInstaller` is documented as a non-release test only.

## Release assets
- [ ] Portable ZIP launches from a fresh extracted folder and retains portable data locally.
- [ ] Installer compiles with Inno Setup 6 or 7 and installs, upgrades, launches, and uninstalls correctly.
- [ ] Inno Setup detection is tested through the installed environment (PATH, registry, or standard location).
- [ ] `artifacts/SHA256SUMS.txt` contains every distributed ZIP and EXE.
- [ ] `.\scripts\Build-Checksums.ps1 -Verify` succeeds.
- [ ] Asset names and embedded versions match the release tag.

## Runtime regression
- [ ] Installed and portable startup pass.
- [ ] Immediate-close startup test passes.
- [ ] Dashboard, server controls, workspace, backups, players, guilds, bases, MODs, and configuration pass.
- [ ] World Management, Repair Center, Transaction Center, and Diagnostics Center pass.
- [ ] Notification self-test passes and final notification hides the bell.
- [ ] Support package is reviewed for redaction and privacy.

## UI
- [ ] Pages reviewed at 100%, 125%, and 150% scaling.
- [ ] Buttons, tooltips, cards, dialogs, and status colors follow MystTiq standards.
- [ ] No dead controls, duplicate navigation entries, clipped labels, or placeholder actions remain.

## Documentation and GitHub
- [ ] README and contributor build instructions match the current scripts.
- [ ] CHANGELOG is consolidated only during the explicit “wrap it up” closeout.
- [ ] GitHub Pages, release assets, tag decision, commit message, and GitHub Release checklist are finalized only during closeout.
- [ ] The validated release is promoted as the official baseline only after compile and runtime approval.


## Current baseline / target validation
- Official baseline: v0.2.15.7 — Unified Runtime State Architecture.
- Current target: v0.2.15.8 — Runtime Evidence Engine Refinement.
- [ ] `ModRuntimeEvidenceEngine` is the single verification interpreter for UE4SS/Lua positive runtime evidence.
- [ ] AntiDupe and PalImportFilter no longer remain Runtime Unverified when the MOD Library has authoritative Loaded evidence.
- [ ] Expanded positive signatures are session-scoped through `RuntimeStateService`; historical logs cannot create cross-session Loaded state.
- [ ] RuntimeStateService evidence changes synchronize Library and Dashboard without requiring a manual Verify All timing window.
- [ ] Verification details/export identify the runtime evidence source and matched alias where available.
- [ ] Stop clears runtime state; next start reacquires evidence without prior-session leakage.
- [ ] REFRESH INFO remains a local metadata/runtime refresh and SEARCH ONLINE remains the only browser-search action.
- [ ] Build Clean / Validate / All passes.
- [ ] v0.2.15.8 logic harness passes with zero failures.
- Phase-specific release notes, build test plan, and apply instructions are maintained under `release-notes/`.


## v0.2.15.8 FIX1 acceptance
- [ ] 0 build errors / 0 warnings
- [ ] Missing positive evidence displays Active / Unverified, not Not loaded
- [ ] Positive current-session evidence produces Loaded / Healthy
- [ ] AntiDupe and PalImportFilter semantics verified
- [ ] Export includes confidence/source detail


## v0.2.15.9 acceptance
- [ ] Clean / Validate / All pass with zero errors and warnings.
- [ ] Logic harness passes.
- [ ] UE4SS verification details include capability profile.
- [ ] Active / Unverified remains non-failure for quiet mods.
- [ ] Observed functional activity can promote a mod to Confirmed Running.
- [ ] Export contains capability/evidence details.


## v0.2.15.10 acceptance
- [ ] Clean / Validate / All: zero errors/warnings.
- [ ] Native runtime logic harness passes.
- [ ] AntiDupe exact DLL path confirms Loaded.
- [ ] PalImportFilter exact DLL path confirms Loaded.
- [ ] Duplicate `main.dll` filenames do not cross-confirm.
- [ ] Stop/start clears old session proof.
- [ ] Disabled native mod is not confirmed.
- [ ] Module inspection unavailable => Active / Unverified.
- [ ] Lua evidence behavior unchanged.


## v0.2.15.10 FIX1 release synchronization
- [ ] `Build.ps1 Validate` reports 0 errors / 0 warnings.
- [ ] Only the v0.2.15.10 logic harness remains active under `scripts`.
- [ ] `docs/index.html` references v0.2.15.10.
- [ ] `src/PalworldManager/app.manifest` references v0.2.15.10.
- [ ] v0.2.15.10 harness passes without PowerShell alias collisions.
- [ ] Native runtime module detection behavior remains unchanged from v0.2.15.10.


## v0.2.15.11 modularization acceptance
- [ ] Clean / Validate / All passes with 0 errors and 0 warnings.
- [ ] v0.2.15.11 architecture harness passes.
- [ ] MOD Library scan behavior matches v0.2.15.10 FIX1.
- [ ] Verify All, Verify Selected, compatibility scan, and export behave identically.
- [ ] Native module evidence still confirms AntiDupe/PalImportFilter when mapped.
- [ ] Runtime-state event updates still synchronize Library/Dashboard.
- [ ] MOD enable/disable/install/delete/repair flows remain unchanged.
- [ ] MystTiq dark-theme/button/tooltip standards unchanged.

## v0.2.15.11 FIX1 compile-hotfix acceptance
- [ ] Clean / Validate / All pass.
- [ ] No multiline-string parser errors in MainWindow.ModCenter.cs.
- [ ] v0.2.15.11 logic harness passes.
- [ ] MOD workflows remain behaviorally equivalent to v0.2.15.10 FIX1.

## v0.2.15.12 acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.12 architecture harness passes.
- [ ] Start / stop / restart behavior unchanged.
- [ ] Existing running PalServer adoption works.
- [ ] Resource and I/O monitoring work.
- [ ] Session ID/PID changes on restart; stale session evidence is cleared.
- [ ] AntiDupe and PalImportFilter native module evidence remains functional.
- [ ] Server update behavior remains unchanged.
- [ ] MystTiq UI/theme/button/tooltip behavior unchanged.

## v0.2.15.12 FIX1 compile-hotfix acceptance
- [ ] Validation reports 0 errors / 0 warnings.
- [ ] ServerService contains no stale extracted process/session helper calls.
- [ ] Clean / Validate / All pass.
- [ ] v0.2.15.12 logic harness passes.
- [ ] Runtime behavior remains equivalent to v0.2.15.11 FIX1.

## v0.2.15.13 operational-health acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.13 logic harness passes.
- [ ] All-disabled MOD set causes no MOD health deduction.
- [ ] Active / Unverified causes no MOD health deduction.
- [ ] Enabled Failed/Error MOD reduces Overall Health.
- [ ] Enabled confirmed conflict/missing dependency reduces Overall Health.
- [ ] MOD card and compact health strip use centralized health wording.
- [ ] Overall Health tooltip marks neutral MOD state informationally.
- [ ] Start/stop/restart/native runtime evidence remain functional.

## v0.2.15.13 FIX1 conflict-health acceptance
- [ ] 8 Healthy MODs with `No known conflict` => 0 confirmed MOD issues.
- [ ] Disabled MODs remain neutral to Overall Health.
- [ ] Explicit `Confirmed conflict` still counts as a real issue.
- [ ] Runtime errors / failed / missing / misconfigured / missing dependency behavior remains intact.
- [ ] Clean / Validate / All and logic harness pass.

## v0.2.15.14 acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.14 architecture harness passes.
- [ ] MainWindow no longer constructs core server/MOD graph directly.
- [ ] ApplicationServiceComposition constructs core graph.
- [ ] ServerService depends on IServerSessionInspector.
- [ ] Windows ServerSessionInspector behavior unchanged.
- [ ] Start / stop / restart / adoption work.
- [ ] Native MOD evidence remains functional.
- [ ] v0.2.15.13 operational-health behavior remains intact.

## v0.2.15.15 acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.15 platform-abstraction harness passes.
- [ ] ServerService depends on IServerPlatformOperations.
- [ ] WindowsServerPlatformOperations preserves executable resolution and launch settings.
- [ ] Normal start / stop / restart work.
- [ ] Force Stop removes the owned process tree.
- [ ] Existing running PalServer adoption works.
- [ ] Native MOD evidence remains functional.
- [ ] Operational Health behavior remains unchanged.

## v0.2.15.16 acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.16 harness passes.
- [ ] ServerService contains no hard-coded PalServer-Win64 process names.
- [ ] Windows profile preserves existing process/executable conventions.
- [ ] Start / stop / restart / force-stop / adoption work.
- [ ] CPU/RAM and port detection remain correct.
- [ ] Native MOD evidence and operational health remain unchanged.
- [ ] README contains one authoritative current baseline/RC statement.
- [ ] README has no duplicate Feature Matrix or appended release diary.
- [ ] README does not claim Linux support is currently released.

## v0.2.15.17 acceptance
- [ ] Clean / Validate / All: 0 errors / 0 warnings.
- [ ] v0.2.15.17 logic harness passes.
- [ ] WORLD PULSE renders within the existing Dashboard without clipping.
- [ ] Saved world day/time agrees with the in-game clock after a save.
- [ ] World clock shows unavailable rather than estimating when evidence is absent.
- [ ] PalServer session uptime is correct and resets on restart.
- [ ] Player peak/joins/leaves/unique counters are session-scoped and do not double-count refreshes.
- [ ] Save freshness and latest backup age are correct.
- [ ] Join/leave/day-transition Activity events do not duplicate.
- [ ] MOD runtime evidence and Overall Health remain unchanged.
