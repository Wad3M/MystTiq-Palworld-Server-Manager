<!-- MystTiq v0.9.2.0: file reviewed for this release (2026-09-28). -->
# v0.9.1.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.1.0.
- New `src/MystTiq.Desktop/Services/MessageCatalog.cs`: English message → chosen language (exact, templates with
  values, open-ended messages, line by line), cached per language.
- `src/MystTiq.Desktop/Services/Localizer.cs`: `Messages` rebuilt on every language change, `Translate`, `Localizer.T`,
  and the `{services:TrText Path}` markup extension.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 1,113 `msg.*` messages in all 12 languages (2,111 keys each).
- `src/MystTiq.Desktop/MainWindow.axaml`, `Views/*.axaml`: 481 display bindings through `{services:TrText}`.
- `src/MystTiq.Desktop/App.axaml`: a String data template, so plain text in drop-downs and lists follows the language.
- `src/MystTiq.Desktop/MainWindow.axaml.cs`, `Views/Confirm*.axaml.cs`, `Views/Select*Dialog.axaml.cs`,
  `Views/TrayReminderToast.axaml.cs`: dialog texts and file-picker titles through `Localizer.T`.
- `scripts/Update-MystTiqUiText.ps1`: `-ConvertBindings`; `-Check` also fails on an untranslated display binding; the
  inventory counts catalog messages as translated.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: catalog checks, the per-language template round trip, and the
  leftover scan covering messages.
- `scripts/Test-v0.9.1.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.1.0.
- Docs: `docs/architecture/v0.9.1.0-translated-messages.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`, `docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`,
  `docs/release/README.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`.
