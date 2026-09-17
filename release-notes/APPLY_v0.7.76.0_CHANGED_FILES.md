# Apply v0.7.76.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.76.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.76.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change and no wire-contract change (no new routes). Touches `MystTiq.Desktop` only: new `Views/SelectGuildDialog.*`, `Views/ConfirmOperationDialog.*`, `MainWindow.axaml`/`.axaml.cs` (context menus + handlers), `ViewModels/MainWindowViewModel.cs` (new Preview/Apply method pairs calling existing API client methods). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
