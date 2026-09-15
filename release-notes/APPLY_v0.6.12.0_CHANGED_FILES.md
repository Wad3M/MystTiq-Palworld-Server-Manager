# Apply v0.6.12.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.12.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.12.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. No new routes. Every existing route's happy-path behavior is unchanged; both fixes in this release only change behavior in previously-broken cases.

If you have automation rules with an idle-threshold trigger, they continue to work exactly as before (the scheduling logic was already safely clamping invalid values) — this release does not change stored rule data.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
