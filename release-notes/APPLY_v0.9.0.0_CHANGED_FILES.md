<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# v0.9.0.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.0.0.
- `src/MystTiq.Desktop/MainWindow.axaml`, `App.axaml`, `Views/*.axaml`: every hard-coded display text replaced by
  `{services:Tr ui.…}` (932 places); the Notifications button is a bell; a language picker right of Settings
  (`TitleLanguagePicker`).
- `src/MystTiq.Desktop/Assets/i18n/en.json` (998 texts), `de.json`, `es.json` (completed), new `zh-Hans.json`,
  `pt-BR.json`, `ru.json`, `fr.json`, `ja.json`, `ko.json`, `it.json`, `pl.json`, `tr.json`.
- `src/MystTiq.Desktop/Services/Localizer.cs`: 12 languages; per-language font chains applied on every change.
- `src/MystTiq.Desktop/App.axaml`, `Styles/DesignSystem.axaml`: fonts read `UiFontFamily`, `UiDisplayFontFamily`,
  `UiDisplayTextFontFamily`.
- New `scripts/Update-MystTiqUiText.ps1`; new `docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: every language through the layout checks; English-leftover scan;
  title-bar picker, bell and font checks.
- `scripts/Test-v0.9.0.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.0.0.
- Docs: `docs/architecture/v0.9.0.0-display-languages.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` (translation split into v0.9.0.0 / v0.9.1.0 / v0.9.2.0),
  `docs/release/README.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`.
