# v0.8.25.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.25.0.
- New: `src/MystTiq.Desktop/Services/DecorativePalette.cs` (the decorative colours, their roles and per-mode rules)
  and `src/MystTiq.Desktop/Services/SystemContrastPalette.cs` (Windows contrast themes).
- `src/MystTiq.Desktop/Services/ThemeApplier.cs`:
  - writes the decorative colours and shadows, the three remaining keyed gradients, and the button text;
  - uses a Windows contrast theme in High contrast and Follow the system;
  - `ResolveVariant`.
- `src/MystTiq.Desktop/Styles/DesignSystem.axaml`, `src/MystTiq.Desktop/MainWindow.axaml`: literal colours replaced by
  resources.
- `src/MystTiq.Desktop/Controls/ResourceHistoryChart.cs`, `HostHistoryChart.cs`: themed colours; redraw on theme change.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - the Light check uses `ResolveVariant`;
  - High contrast follows system colour changes too.
- HOST art (the user's): new `src/MystTiq.Desktop/Assets/Icons/icon-host.png`, `Assets/Artwork/page-art-host-dark.png`,
  `page-art-host-light.png`; `Services/ArtworkCatalog.cs` (Host → host) and the nav item in `MainWindow.axaml`.
- Tests: `scripts/Testing/MystTiq.ArtworkHarness/Program.cs` (19 checks), `scripts/Test-v0.8.25.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.25.0-theme-leftovers.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md` (accepted baseline; Highlights and Features brought up to date),
    `docs/index.html` (baseline; three feature cards), `docs/roadmap/PRODUCT_ROADMAP.md`;
  - `.gitignore`: local and third-party files kept out of the repository (`.claude/`, `*.obj`, the UE4SS zip, the
    map originals, stale root notes).
- `.github/workflows/build.yml`: also builds the headless service and the desktop, on Windows and Linux (the legacy WPF
  build stays). Not run here: GitHub Actions runs it once the repository is on GitHub.
