<!-- MystTiq v1.0.0.2: file reviewed for this release (2026-10-05). -->
# v0.9.1.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Update-MystTiqUiText.ps1 -Check`: no hard-coded XAML text and no untranslated display binding left.
3. `scripts/Test-v0.9.1.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.0.0 gate (every earlier contract);
   - the v0.9.1.0 contracts: the `msg.*` catalog in every language, data kept English, `MessageCatalog` and
     `{services:TrText}` (never on an editable field), the String data template, `Localizer.T` in dialogs and file
     pickers, the inventory, the review stamp at v0.9.1.0;
   - the ArtworkHarness in every language: exact, template, reordered, open-ended and multi-line messages; unknown text
     and English unchanged; view models stay English; every template round-trips; no untranslated label or message on
     any page; the v0.9.0.0 layout checks;
   - every carried smoke and the Linux VM checks.
4. Publish, launch, check `/healthz` reports 0.9.1.0; switch to Japanese from the title bar and look at the Dashboard's
   status values, then switch back to English.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
