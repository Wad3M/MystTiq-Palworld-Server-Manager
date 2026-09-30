<!-- MystTiq v1.0.0.0: file reviewed for this release (2026-09-30). -->
# v0.9.3.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Update-MystTiqUiText.ps1 -Check`: no hard-coded XAML text and no untranslated display binding left.
3. `scripts/Test-v0.9.3.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.2.0 gate (every earlier contract);
   - the v0.9.3.0 contracts: at least 2,400 messages, every one present in all 11 languages, the service's messages
     among them, server commands and Windows names left out, the review export and checklist, the review stamp at
     v0.9.3.0;
   - the ArtworkHarness's service-message checks in Japanese and every template's round trip (now 2,492 messages);
   - every carried smoke and the Linux VM checks.
4. `scripts/Export-MystTiqTranslationReview.ps1`: a sheet per language with every text.
5. Publish, launch, check `/healthz` reports 0.9.3.0; switch to Japanese, open Diagnostics and run Doctor (its findings
   are in Japanese), then switch back to English.
6. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
7. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
