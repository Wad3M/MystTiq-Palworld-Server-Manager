# Apply v0.5.1.0 Changed Files

The Changed Files ZIP is an audit and review artifact for the promoted v0.4.18.2 functional baseline. Normal installation should use `MystTiqPalworldServer_v0.5.1.0_FullSource.zip` with `Update-FromDownloads.ps1`; that workflow closes only artifact-hosted MystTiq processes, replaces the staged source safely, and runs the complete current-version gate before relaunch.

Do not overlay the Changed Files ZIP onto v0.4.17.4 or an unknown tree. This release deliberately preserves the newer v0.4.18.2 functionality.
