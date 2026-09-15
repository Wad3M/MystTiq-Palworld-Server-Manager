# Apply v0.7.15.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.15.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.15.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No breaking configuration schema change: a new `players/temporary-bans.json` file is created automatically under each server profile's runtime root on first use, the same way `players/whitelist.json` was in v0.7.10.0. Touches `MystTiq.Core` (new `TemporaryBanModels.cs`), `MystTiq.HeadlessHost` (new `HeadlessTemporaryBanService.cs`, `ServerProfileHost.cs`, `LocalManagementApiHost.cs`, `HeadlessHistoricalMetricsService.cs`), and `MystTiq.Desktop` (new temp-ban DTOs/client calls/ViewModel state, `ResourceHistoryChart.cs`'s new FPS series, `MainWindow.axaml`'s new controls and stub removals).

Do not overlay it onto v0.4.17.4 or an unknown source tree.
