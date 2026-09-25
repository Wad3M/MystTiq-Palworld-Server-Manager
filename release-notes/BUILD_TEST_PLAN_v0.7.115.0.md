# v0.7.115.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean (hygiene included): 0 errors / 0 warnings.
3. Run `scripts/Test-v0.7.115.0-Logic.ps1 -RunBuild`. This is the first gate expected to pass with **no** failures:
   in-gate validation uses `-AllowBuildOutputs`. If the v0.7.114.0 checkpoint ZIP is not on the machine, the
   frozen-baseline check shows as SKIP (pass `-FrozenBaselineZip` to point at a copy).
4. New surface: rectangle map-label layout and label layers, `SupervisorRecoveryStateStore`, readiness before
   recovery success, operation-journal reload, `Validate-Release -AllowBuildOutputs`, the framework's SKIP state.
5. Live: the Map page shows every player and base name next to its own marker with nothing overlapping, at
   whole-map zoom and zoomed in.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
