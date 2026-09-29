<!-- MystTiq v0.9.7.0: file reviewed for this release (2026-09-29). -->
# v0.9.4.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Update-MystTiqUiText.ps1 -Check`: no hard-coded XAML text and no untranslated display binding left.
3. `scripts/Test-v0.9.4.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.3.0 gate (every earlier contract);
   - the v0.9.4.0 contracts: the role hint (tooltip shown while disabled, help text, own tip restored, Ribbon left
     alone), every list/text box/drop-down/number box/slider/check box in the XAML named, the role hints and health
     states in the catalog, `TrFormat` translating its value, the review stamp at v0.9.4.0;
   - the ArtworkHarness: for Viewer, Operator, Admin and Owner on every page, each control disabled for the role names
     it and no allowed one does; the hint in Japanese and gone again for Admin; every control named and reachable with
     Tab on every page; every template's round trip;
   - every carried smoke and the Linux VM checks.
4. Publish, launch, check `/healthz` reports 0.9.4.0; in Japanese the Dashboard's health state and MOD health are
   Japanese; then switch back to English.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
