<!-- MystTiq v0.9.7.0: file reviewed for this release (2026-09-29). -->
# v0.9.3.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.3.0.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: the service's own messages (HeadlessHost and Core) as 1,339 more `msg.*`
  keys (3,490 keys each). Server commands, log patterns, protocol text and Windows service/firewall-rule names are left
  out on purpose.
- `src/MystTiq.Desktop/Services/MessageCatalog.cs`: a whole-line template is tried before the sentences, and is
  accepted only when none of its values spans a sentence break.
- `scripts/Export-MystTiqTranslationReview.ps1` (new): a review sheet per language.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: service-message checks in Japanese; the template round trip
  gives a message that begins a longer text a rest to match.
- `scripts/Test-v0.9.3.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.3.0.
- Docs: `docs/architecture/v0.9.3.0-the-service-s-messages.md`, `docs/i18n/TRANSLATION_REVIEW.md` (new), the
  release-notes trio, `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`,
  `docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`, `docs/release/README.md`, `RELEASE_CHECKLIST.md`,
  `docs/release/pre-publication-review.md`.
