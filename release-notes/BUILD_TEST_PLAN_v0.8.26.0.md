<!-- MystTiq v0.9.2.0: file reviewed for this release (2026-09-28). -->
# v0.8.26.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors and 0 warnings. Validation now requires
   the product files only (no legacy WPF app) and reads the version from `src/MystTiq.Desktop/app.manifest`.
2. `dotnet build PalworldServerManager.slnx -c Release`: the solution holds Core, HeadlessHost and Desktop.
3. `scripts/Test-v0.8.26.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.25.0 gate (every earlier contract, relabelled as regression);
   - the v0.8.26.0 contracts: the legacy app and its tools are gone and nothing current refers to them, the removed
     files are listed in the audit, every text file carries the review stamp, the live scripts exist and parse;
   - both harnesses and every carried smoke;
   - the live-script rehearsal (`Test-v0.8.26.0-LiveScriptsRehearsal.ps1`): the in-game and alerts scripts against
     stand-in RCON, REST and webhook servers;
   - the Linux VM checks, including the real desktop session, when the VM answers.
4. Publish the desktop build, launch it and check `/healthz` reports 0.8.26.0.
5. `Build.ps1 Package`: the Windows and Linux ZIPs and `SHA256SUMS.txt` are produced and verify.
6. Live, when available (reported as pending otherwise, never as passed):
   - `Test-v0.8.26.0-InGame.ps1` with a player on the clone server;
   - `Test-v0.8.26.0-Alerts.ps1` (sends real messages);
   - `Test-v0.8.26.0-ContrastTheme.ps1` with a Windows contrast theme on.
7. Create the FullSource ZIP checkpoint and verify it against its SHA-256 manifest.

Any failure in steps 1 to 5 blocks the release.
