<!-- MystTiq v1.0.0.4: file reviewed for this release (2026-10-05). -->
# v0.9.9.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.9.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.8.0 gate (every earlier contract);
   - the v0.9.9.0 contracts: the helper is reused and stopped without its process tree, the exit-after-join signature
     and detector, the UE4SS content match, the Doctor's port fix, formats per language, the review stamp at v0.9.9.0;
   - the logic harness's new scenarios (the helper record, exit after join, the UE4SS match) and the ArtworkHarness's
     format checks;
   - `Test-v0.9.9.0-RouteSmoke.ps1` (the Doctor's port fix end to end; crash analysis on real log lines);
   - every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Live: the Update Center's UE4SS row on a real install; the Windows app reusing its helper across a restart.
5. Publish, launch, check `/healthz` reports 0.9.9.0.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.