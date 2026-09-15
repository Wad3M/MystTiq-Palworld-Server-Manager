# Apply v0.7.50.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.50.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.50.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches only `MystTiq.HeadlessHost` (`HeadlessMonitoringService.cs` — two new candidate paths in `ResolveConsoleSources`, no other changes). No change to `MystTiq.Core` or `MystTiq.Desktop`.

This release is purely additive — it widens which log files the Console page's merged view can read from; it does not change any existing behavior, route, or DTO shape.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
