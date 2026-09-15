# Apply v0.7.71.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.71.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.71.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change and no wire-contract change. Touches `MystTiq.HeadlessHost` only: `HeadlessMonitoringService.cs` (`ResolveConsoleSources` gains a `"PalDefender log"` source). No change to `MystTiq.Core` or `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
