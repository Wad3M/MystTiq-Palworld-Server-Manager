# v0.8.0.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean (also removing
   `scripts/Testing/MystTiq.ArtworkHarness/bin` and `obj`), then PUBLISH the desktop build (with `Select-Object -Last`,
   never `-First`).
2. `scripts/Validate-Release.ps1 -Strict` after Clean: 0 errors / 0 warnings.
3. `scripts/Test-v0.8.0.0-Logic.ps1 -RunBuild`, including the frozen v0.7.115.0 gate, every carried smoke (now including
   v0.7.115.0's), the logic harness, and `MystTiq.ArtworkHarness` (149 offline rendering checks).
4. Live: Dashboard, a World page and the Map page in the Desktop show the new icons and category art, and the Map's
   labels are still placed beside their markers without overlap. Light mode is covered by the artwork harness renders.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
