# Apply v0.7.61.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.61.0 source baseline.
Normal installation should use `MystTiqPalworldServer_v0.7.61.0_FullSource.zip` with
`Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly,
and the complete current-version gate runs before relaunch.

No configuration schema change, no new routes. Touches `MystTiq.Core`
(`Models/HeadlessConfiguration.cs`, `Services/HeadlessConfigurationService.cs`) only. No
`MystTiq.HeadlessHost` or `MystTiq.Desktop` changes.

**After applying**: restart the headless host process (or the whole app) so the updated
configuration-loading logic actually runs and backfills `-unattended` into any already-persisted
server profile. A config file edited directly, or a process that hasn't restarted since this
version was applied, won't pick up the fix.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
