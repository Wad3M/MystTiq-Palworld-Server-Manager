<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# v0.9.0.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Update-MystTiqUiText.ps1 -Check`: no hard-coded XAML text left.
3. `scripts/Test-v0.9.0.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.26.0 gate (every earlier contract);
   - the v0.9.0.0 contracts: 12 languages with every English key and the same placeholders, no hard-coded XAML text,
     the per-language fonts, the title-bar picker and bell, the review stamp at v0.9.0.0;
   - the ArtworkHarness in every language (tabs fit at 950x650, Ribbon not cut off, Dashboard labels intact, no English
     leftovers on any page, the title-bar picker, Japanese font);
   - every carried smoke and the Linux VM checks.
4. Publish, launch, check `/healthz` reports 0.9.0.0; switch languages from the title bar and look at the window in
   Chinese, Japanese, Korean and Russian.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
