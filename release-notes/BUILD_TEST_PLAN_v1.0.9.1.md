<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-23). -->
# v1.0.9.1 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.9.1-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.9.0 gate, every earlier contract, the v1.0.9.1
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. B-4: the four LogicHarness scenarios on `DiscordCommandFlow` pass (refusal, acknowledge before work, private failure
   reason, expired reply). Live with the owner's bot: `/mysttiq-backup` and `/mysttiq-restart` show their results.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.9.1-Logic.ps1` there (static).
