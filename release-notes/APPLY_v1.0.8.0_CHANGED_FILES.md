<!-- MystTiq v1.0.8.5: file reviewed for this release (2026-10-12). -->
# v1.0.8.0 Changed Files

- `src/MystTiq.Desktop/ViewModels/SessionContext.cs` (new): `SessionContext` and `PageViewModel`.
- `src/MystTiq.Desktop/ViewModels/ConsoleLogViewModel.cs` (new): the Console page and the log lines, moved.
- `src/MystTiq.Desktop/ViewModels/Commands.cs` (new): `RelayCommand`, `RelayCommand<T>` and `IRoleGatedCommand`, moved.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the session and the Console ViewModel created, pages told
  when shown, the moved code removed. `MainWindow.axaml` binds `Logs.*`; `MainWindow.axaml.cs` exports
  `Logs.FilteredLogLines`.
- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs`: PalServer in its own process group.
- `src/MystTiq.HeadlessHost/ConsoleCtrlC.cs` (new) and `Program.cs`: Ctrl+C turned back on at start-up.
- `scripts/Testing/Get-MystTiqCommandRoles.ps1`: reads every ViewModel file, follows `Session.Api` calls.
- Tests: `scripts/Test-v1.0.8.0-Logic.ps1` (generated from the v1.0.7.5 gate; the console contract follows the code),
  a Ctrl+C check in the long set, `Send-MystTiqConsoleStop.ps1 -Signal CtrlC`, the ArtworkHarness reading `Logs`.
