# Apply v0.7.12.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.12.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.12.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release adds test infrastructure only (`scripts/Testing/MystTiq.LogicHarness/`, `scripts/Test-v0.7.12.0-RouteSmoke.ps1`) plus a one-line stale-comment fix in `MystTiq.Core` — no product behavior changed in `MystTiq.Core`, `MystTiq.HeadlessHost`, or `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
