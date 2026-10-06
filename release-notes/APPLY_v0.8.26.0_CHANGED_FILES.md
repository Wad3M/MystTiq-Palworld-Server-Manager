<!-- MystTiq v1.0.3.0: file reviewed for this release (2026-10-05). -->
# v0.8.26.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest` (new; the Desktop's own manifest, which now carries the
  version): version bump to 0.8.26.0. `src/MystTiq.Desktop/MystTiq.Desktop.csproj` references the manifest.
- Removed: the legacy WPF app, its installer and tools, old tests, pre-0.8 change lists and test plans, parity notes and
  leftovers, about a thousand files in all. The per-file list is `docs/release/FILE_AUDIT_v0.8.26.0.md`
  (and `.csv`).
- Every text file: the v0.8.26.0 review stamp (`scripts/Set-MystTiqFileStamp.ps1`, new).
- Build and release:
  - `PalworldServerManager.slnx`: Core, HeadlessHost and Desktop;
  - `Build.ps1`, `scripts/Build.ps1`, `scripts/Build-Release.ps1`: the product only; `Package` makes both platforms' ZIPs;
  - `scripts/Validate-Release.ps1`: product files, the Desktop manifest, no WPF-only checks; the stale-version scan
    compares the four-part number, so `Test-v0.8.26.0-InGame.ps1` is not mistaken for a pre-release tag;
  - `scripts/Build-ConsoleProxy.ps1`: the compiler's object file goes to `artifacts\native` (it used to land in the
    repository root as `dllmain.obj`);
  - `.gitignore` (Python caches), `.gitattributes` (`.sh`/`.py` stay LF).
- The user's v0.8.25.0 GitHub edits, merged: `README.md`, `RELEASE_CHECKLIST.md`, `.github/workflows/build.yml`,
  `release.yml`, `docs/index.html`, `docs/history/*`, `docs/release/*`, `docs/roadmap/*`, `release-notes/v0.8.25.0.md`
  (its garbled title dash fixed), `scripts/Package-GitHubRelease.ps1`, the banner image.
- New live scripts: `scripts/Test-v0.8.26.0-LinuxDesktopSession.ps1`, `-InGame.ps1`, `-Alerts.ps1`,
  `-ContrastTheme.ps1`, and the gate's `-LiveScriptsRehearsal.ps1`.
- `scripts/Testing/MystTiq.RemoteSignInHarness/Program.cs`: the real-window mode (X11 window, clipboard, tray checks).
- `scripts/Test-v0.8.24.0-LinuxIsolated.sh`/`.ps1`: the expected version is passed in instead of fixed.
- `docs/artwork/ASSETS.json`: lists the HOST art and icon.
- Stale references to the removed app fixed: `CONTRIBUTING.md`, `README.md`, `docs/linux/README.md`,
  `docs/release/pre-publication-review.md`, `src/MystTiq.Core/Models/ServerRuntimeConfiguration.cs` (comment).
- Docs: `docs/architecture/v0.8.26.0-repository-cleanup.md`, `docs/release/README.md` (the publishing guide, rewritten
  step by step), the release-notes trio, `CHANGELOG.md`, `README.md`, `docs/index.html`, the roadmap and checklist.
- Tests: `scripts/Test-v0.8.26.0-Logic.ps1`.
