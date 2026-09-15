# Apply v0.7.28.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.28.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.28.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `Models/ServerStatusDto.cs` (new `ServerProcessDto.DisplayText`); `ViewModels/MainWindowViewModel.cs` (`ForceStopServerCommand`/`InstallMissingEnvironmentCommand`, `ManagedProcesses`/`HasManagedProcesses`, `ApplyStatus` extended, `BuildRibbonGroupsForActivePage` extended); `MainWindow.axaml` (Server Doctor's status card gains a process list). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost` — the process data itself was already flowing over the wire before this release.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
