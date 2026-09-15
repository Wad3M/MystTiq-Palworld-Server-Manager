# Apply v0.7.65.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.65.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.65.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Linux-only behavior change: a running Linux install upgrading to this version will start writing its console capture to `<ServerRoot>/Pal/Saved/Logs/MystTiq-PalServer-Console.log` instead of `<ManagerRuntimeRoot>/palserver-console.log` from the next server start onward — the old file is left in place, untouched, not migrated. Touches `MystTiq.Core` (`Services/LinuxServerLifecycleService.cs`) and adds `scripts/Test-v0.7.65.0-LinuxAcceptance.sh`/`scripts/Test-v0.7.65.0-ProductionReadiness.sh` (carried forward from v0.7.62.0's copies).

Do not overlay it onto v0.4.17.4 or an unknown source tree.
