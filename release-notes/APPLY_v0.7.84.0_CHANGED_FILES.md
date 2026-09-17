# Apply v0.7.84.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.84.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.84.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.Desktop` only: `ViewModels/MainWindowViewModel.cs` (`CloneWorldNeedsRestart`/`CloneWorldNewProfileId`/`RestartAfterCloneCommand`/`RestartAfterCloneAsync`, `RaiseIsBusyDependents` extended); `MainWindow.axaml` (new "Restart MystTiq Now" button on the Clone World card). The Clone World workflow card itself (source/new-profile/port-offset sections) was already built in an earlier session and is unchanged by this release. No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
