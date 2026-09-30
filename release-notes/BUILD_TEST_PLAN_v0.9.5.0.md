<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# v0.9.5.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.5.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.4.0 gate (every earlier contract);
   - the v0.9.5.0 contracts: the unreadable-process rule on Windows and Linux, paths by image name, the expected port
     from `-port=`, SteamCMD's public build (no `UpToDateCheck`), the PalDefender row, the refused-manifest retry and
     SteamCMD's reason, the two Doctor findings, the review stamp at v0.9.5.0;
   - the logic harness's six new scenarios;
   - `Test-v0.9.5.0-Upgrade.ps1` (v0.8.25.0 → this version → v0.8.25.0, backup/restore, fresh setup) and
     `Test-v0.9.5.0-FleetRecovery.ps1` (two servers in one service);
   - every carried smoke, both harnesses and the Linux VM checks.
3. Live, with the new build's service: stop and start a real server and confirm no other profile starts or logs a
   crash; read the Update Center and the Doctor for a current and an out-of-date server.
4. Publish, launch, check `/healthz` reports 0.9.5.0.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
