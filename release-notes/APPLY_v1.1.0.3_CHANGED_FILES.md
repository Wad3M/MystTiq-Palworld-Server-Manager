<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-22). -->
# v1.1.0.3 Changed Files

- `src/MystTiq.HeadlessHost/HeadlessPlayerIdService.cs`: the old character file is removed after a verified move (a
  working copy restores it on failure); `CleanUpOldEntries` removes v1.1.0.2 archived files a backup holds and tidies the
  player list.
- `src/MystTiq.HeadlessHost/HeadlessPlayerRegistryService.cs`: no "None" records; `MergeInto` and `CleanUp`.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: the player list given to the move; the clean-up run once at start.
- Tests: `scripts/Test-v1.1.0.3-Logic.ps1`, a new LogicHarness scenario and the real-world move scenario updated.
