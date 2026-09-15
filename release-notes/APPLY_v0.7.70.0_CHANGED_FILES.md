# Apply v0.7.70.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.70.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.70.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No breaking configuration/wire-contract change — `NotificationChannelConfig` gained new optional fields only; every existing construction/persisted `channels.json` still loads and works unchanged. Touches `MystTiq.HeadlessHost` only: `HeadlessNotificationRoutingService.cs`. Also touches `scripts/Testing/MystTiq.LogicHarness/Program.cs` (test coverage only). No change to `MystTiq.Core` or `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
