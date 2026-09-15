# Apply v0.6.15.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.15.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.15.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. New routes: `GET /pals`, `POST /pals/edit/preview`, `POST /pals/edit/apply` (all additive). Every existing route is unchanged.

The new Pal Editor mutates the active world's `Level.sav` the same way Guild/Base Ownership repair already does — it requires PalServer to be stopped, and creates a fresh safety backup before every applied change.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
