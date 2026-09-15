# Apply v0.6.13.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.13.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.13.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. No new routes. New CLI option: `--server-id <id>`, accepted by `service-install`/`service-uninstall`/`service-status`/`service-run`/`status`/`start`/`stop`/`restart` (additive, omitting it keeps prior behavior).

If you have MystTiq installed as a Windows Service or systemd unit for your default server, its service/unit name is unchanged by this release and will not be touched, renamed, or need reinstalling.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
