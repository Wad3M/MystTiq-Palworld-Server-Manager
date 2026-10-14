<!-- MystTiq v1.1.0.1: file reviewed for this release (2026-10-13). -->
# v1.0.9.1 Changed Files

- `src/MystTiq.HeadlessHost/DiscordCommandFlow.cs` (new): the order of one slash command (refuse, or acknowledge, work,
  then edit), apart from the gateway so it is tested.
- `src/MystTiq.HeadlessHost/HeadlessDiscordBotService.cs`: commands run through the flow; the real reply keeps mentions off;
  start and restart run the pre-start MOD check.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: the bot is given the MOD service's pre-start check.
- Tests: `scripts/Test-v1.0.9.1-Logic.ps1` and four LogicHarness scenarios on the flow.
