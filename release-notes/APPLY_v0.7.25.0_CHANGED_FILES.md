# Apply v0.7.25.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.25.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.25.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: new `Models/RibbonActionViewModel.cs`; `ViewModels/MainWindowViewModel.cs` (ribbon group construction, shrink-to-fit layout, `RaisePageVisibility`/constructor hooks); `MainWindow.axaml` (ribbon converted to a data-bound `ItemsControl` with an overflow button); `MainWindow.axaml.cs` (`RibbonHost_OnSizeChanged`, `RibbonOverflowButton_OnClick`); `Styles/DesignSystem.axaml` (new `TextBlock.flatIcon.amber`/`.green`/`.blue`/`.red`/`.cyan` style rules). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
