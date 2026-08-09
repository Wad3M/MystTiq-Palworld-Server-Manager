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
