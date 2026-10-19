<!-- MystTiq v1.1.1.1: file reviewed for this release (2026-10-18). -->
# v1.1.1.1 Changed Files

- `Directory.Build.targets` (new): every publish leaves out .NET's `createdump` helper.
- `scripts/Package-GitHubRelease.ps1`: refuses a package (or a signed folder) that holds `createdump`.
- `scripts/Build-DockerImage.ps1`: refuses an image whose service folder holds `createdump`.
- `docs/images/01-dashboard.png`, `03-players.png`, `06-backups.png`, `07-mods.png`: the README screenshots retaken from
  v1.1.1.1 (the real window rendered at 1920x1080 against a demo service on a copy of the world; no addresses shown).
- Tests: `scripts/Test-v1.1.1.1-Logic.ps1` (the static contract, and with `-RunBuild` a Windows and Linux publish of the
  service checked for `createdump` and started).
