<!-- MystTiq v1.0.0.2: file reviewed for this release (2026-10-05). -->
# v0.9.7.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.7.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.6.0 gate (every earlier contract);
   - the v0.9.7.0 contracts: all four Avalonia packages and `Avalonia.Headless` pinned to 12.1.3, no Avalonia 11 names
     left (`Watermark`, `SystemDecorations`, the clipboard's old methods), the harnesses' `NavigationPage` alias,
     the review stamp at v0.9.7.0;
   - the ArtworkHarness in all 12 languages on Avalonia 12;
   - every carried smoke, both harnesses and the Linux VM checks, including the real Linux desktop session.
3. Compare the ArtworkHarness's page renders with v0.9.6.0's.
4. Publish, launch, check `/healthz` reports 0.9.7.0, and look at the live window.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.