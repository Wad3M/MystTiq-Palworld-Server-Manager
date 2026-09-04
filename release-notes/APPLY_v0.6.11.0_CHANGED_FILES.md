# Apply v0.6.11.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.11.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.11.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. New routes: `GET /diagnostics/network/wan`, `POST /diagnostics/network/wan/upnp/repair` (both additive). Every existing route is unchanged.

If an old `mysttiq-server.exe` sidecar from a prior version is still running on the default port when you relaunch, this version will no longer attach to it silently — it starts its own backend on a free loopback port instead and tells you the old one is still running. Close the old process manually once you've confirmed the new one is healthy.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
