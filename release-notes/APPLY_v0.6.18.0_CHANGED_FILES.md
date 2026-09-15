# Apply v0.6.18.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.18.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.18.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change to `mysttiq.json` in this release. New per-profile file: `anticheat/rules.json` (created automatically with safe, Flag-only defaults on first run). New routes: `GET`/`PUT /anticheat/rules` (Admin-gated), `GET /anticheat/findings` (all additive). Every existing route is unchanged.

Anti-cheat detection is enabled by default but every rule defaults to `Flag` (notify + log only) — no player is auto-kicked or auto-banned on upgrade unless an admin explicitly changes a rule's response.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
