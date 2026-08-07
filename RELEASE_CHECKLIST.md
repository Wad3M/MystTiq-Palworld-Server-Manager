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


## Release Complete
- Documentation audited
- Release notes finalized
- Baseline promoted to v0.2.14.11
