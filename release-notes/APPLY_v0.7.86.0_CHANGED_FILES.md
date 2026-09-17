# Apply v0.7.86.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.86.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.86.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change, no new backend routes/DTOs (the existing Workshop-scan commands are reused unmodified). Touches `MystTiq.Desktop` only: `ViewModels/MainWindowViewModel.cs` (`IsWizardStepMods`, wizard step renumbering: Confirm moved from 7 to 8), `MainWindow.axaml` (new MODs step content, step-count labels updated). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
