# Apply v0.7.85.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.85.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.85.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change, no new routes/DTOs (the existing World Transactions Analyze/Apply routes are reused unmodified). Touches `MystTiq.Desktop` only: `ViewModels/MainWindowViewModel.cs` (`NewServerImportReady`/`NewServerImportPrepareStatusText`/`PrepareForWorldImportCommand`/`PrepareForWorldImportAsync`, `AdvanceWizardStep`/`GoBackWizardStep` skip World Settings choice for Import, `ChooseNewServerWorldSource` resets stale World Transactions state, `AnalyzeWorldArchiveAsync`/`ApplyWorldTransactionAsync` fixed to use `BuildProfileFromEditor`); `MainWindow.axaml` (embedded Analyze/Apply controls replacing the old placeholder text in the Install step). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
