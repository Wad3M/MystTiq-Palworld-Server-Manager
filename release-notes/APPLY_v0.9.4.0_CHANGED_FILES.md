<!-- MystTiq v0.9.6.0: file reviewed for this release (2026-09-29). -->
# v0.9.4.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.4.0.
- `src/MystTiq.Desktop/Services/RoleHint.cs` (new): the role a disabled control needs, in its tooltip (shown while
  disabled) and accessible help text; the control's own tooltip kept and restored.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `MainWindowViewModel.CommandRoles.cs`: gated commands know
  the role they need and the signed-in role; a role change refreshes every hint.
- `src/MystTiq.Desktop/Services/Localizer.cs`: `TrFormat` shows a text value in the language too.
- `src/MystTiq.Desktop/MainWindow.axaml`, `Views/SelectGuildDialog.axaml`, `Views/SelectPlayerDialog.axaml`: about 150
  `AutomationProperties.Name` values.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 21 accessible-name keys, the role hints and 7 health states (3,520 keys
  each).
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: role hints for every role on every page, the hint in Japanese,
  accessible names and Tab reach on every page, health states and label values.
- `scripts/Test-v0.9.4.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.4.0.
- Docs: `docs/architecture/v0.9.4.0-roles-explained-and-accessibility.md`, the release-notes trio, `CHANGELOG.md`,
  `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`, `docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`,
  `docs/release/README.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`.
