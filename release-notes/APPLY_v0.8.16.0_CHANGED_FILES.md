# v0.8.16.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.16.0.
- `src/MystTiq.Desktop/Services/ThemeCatalog.cs`: the five modes, `NormalizeMode`, `BaseVariant`, the Midnight and
  High contrast overrides, `CardBorder`, and the densities.
- `src/MystTiq.Desktop/Services/ThemeApplier.cs`:
  - applies a mode (base palette, overrides, `ModeStop`, `CardBorderBrush`, sidebar sheen);
  - `SystemPrefersLight`, `CurrentMode`;
  - `ApplyDensity`, `CurrentDensity`.
- `src/MystTiq.Desktop/Services/LocalDisplayPreferencesStore.cs` (new): density, in `display-preferences.json`.
- `src/MystTiq.Desktop/Styles/DesignSystem.axaml`: `CardBorderBrush` and the density resources. The base
  Button/TextBox/ComboBox/card/list-row styles read them.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - `SelectedThemeModeIndex`, `SelectedDensityIndex`, `DisplayPreferencesStatusText`;
  - `OnSystemThemeChanged`, subscribed to the platform's colour change;
  - `IsLightMode` is the effective palette;
  - the mode picker refreshes on a tab switch.
- `src/MystTiq.Desktop/MainWindow.axaml`: the Mode and Density pickers replace the Light mode checkbox.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: every mode, following the system, density (isolated store),
  and five renders.
- New: `scripts/Test-v0.8.16.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.16.0-theme-modes.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
