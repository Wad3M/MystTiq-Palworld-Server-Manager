<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# v0.9.10.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.10.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.9.0 gate (every earlier contract), with its one known archive failure (F6) set aside by name;
   - the v0.9.10.0 contracts, one per review finding, plus the translation notes and the stamped checkpoint notes;
   - the logic harness's new scenarios (MOD alert, release channel, stable crash evidence) and the ArtworkHarness's
     (names kept in messages; slow, stuck and reused-id stand-in helpers);
   - every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v0.9.10.0-Logic.ps1` there (static):
   the archive must pass its own gate.
5. Live: the Windows app reusing its helper across a restart; the VM helper on a private port.
6. Publish, launch, check `/healthz` reports 0.9.10.0.
