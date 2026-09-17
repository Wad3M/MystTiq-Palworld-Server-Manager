# Apply v0.7.73.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.73.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.73.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change and no wire-contract change. Touches `MystTiq.Desktop` only: `App.axaml.cs` (`ExitGui_OnClick` now collapses to minimize-to-tray when anything is running instead of always exiting; new `UpdateTrayStatus()` on a 5-second timer keeps the tray tooltip naming what's running). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
