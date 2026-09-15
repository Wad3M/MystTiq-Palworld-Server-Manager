# Apply v0.7.38.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.38.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.38.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (Configuration page: notification strip relocated, World Settings renamed/regrouped into three sub-sections, Advanced Settings now shares the Server Identity/Network cards), `ViewModels/MainWindowViewModel.cs` (`SimpleRateDefinitions` gained a `Group` field, `SimplePalworldSettings` split into three named collections), and `Models/PalworldConfigurationDtos.cs` (`PalworldSimpleSettingItem.Group`). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
