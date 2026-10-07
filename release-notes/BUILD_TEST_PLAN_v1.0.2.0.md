<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.2.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.2.0-Logic.ps1 -RunBuild`: the frozen v1.0.1.0 gate, every earlier contract, the v1.0.2.0
   contracts, every carried smoke, both harnesses and the Linux VM checks (R-3 included, without the reboot).
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.2.0-Logic.ps1` there (static).
5. Live (R-1): the clone started through an isolated service, frozen with `NtSuspendProcess`, restarted by the watchdog,
   with the alert, the Activity entry and the service log line recorded.
6. Live, owed: R-2, one real Discord and one real email send, confirmed arrived. R-3,
   `Test-v1.0.2.0-LinuxSystemd.ps1` with the reboot, on the VM.
