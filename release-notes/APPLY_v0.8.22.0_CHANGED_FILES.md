# v0.8.22.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.22.0.
- `src/MystTiq.Desktop/MainWindow.axaml`: the tab close, settings, minimize, maximize and close icons are `PathIcon`s
  (no Segoe Fluent Icons glyphs).
- `src/MystTiq.Desktop/MainWindow.axaml.cs`: maximize/restore swaps the geometry.
- Tests:
  - `scripts/Testing/MystTiq.RemoteSignInHarness/Program.cs`: the Ribbon on every page, the role-bound page buttons,
    the server agreeing with each kind of gated button, the window's drawn icons, `renderPrefix`;
  - new: `scripts/Test-v0.8.22.0-RemoteSignIn.ps1` (four roles; the harness on Windows and published for linux-x64 on
    the VM), `scripts/Test-v0.8.22.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.22.0-sign-in-tests.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.