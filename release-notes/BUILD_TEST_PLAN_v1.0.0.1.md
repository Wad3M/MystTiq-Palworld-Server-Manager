<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# v1.0.0.1 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.1-Logic.ps1 -RunBuild`: the frozen v1.0.0.0 gate, every earlier contract, the v1.0.0.1 contracts
   (Launcher, manual-parity launch, identity guard, tray and exit, stuck-start protocol, addresses), the stuck-start smoke,
   every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.1-Logic.ps1` there (static).
5. Live: close the window (it goes to the tray), Exit from the tray (no `mysttiq-server` left running); the Dashboard's
   addresses line; a player joining the main server keeps their character.
