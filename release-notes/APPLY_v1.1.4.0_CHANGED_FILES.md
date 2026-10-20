<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.1.4.0 Changed Files

- `src/MystTiq.HeadlessHost/HeadlessServerUpdateFlowService.cs` (new): the Server update card and its update (stop, safety backup, SteamCMD, start again).
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs`: `GetPalworldServerVersionAsync` (the Palworld row alone).
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`, `ServerProfileHost.cs`: `GET /server/update`, `GET /server/update/job`, `POST /server/update` (Admin); the interrupted-stop log line.
- `src/MystTiq.HeadlessHost/PolicyApplyingLifecycle.cs`: `StopInterrupted`.
- `src/MystTiq.Core/Services/ForeignServerProcess.cs` (new), `WindowsServerLifecycleService.cs`, `WindowsServerSessionInspector.cs`: another server MystTiq started is not this one at the wrong path; process parents read.
- `src/MystTiq.Desktop/Models/ServerUpdateDtos.cs` (new), `Services/IMystTiqApiClient.cs`, `Services/MystTiqApiClient.cs`: the card and the update.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ServerUpdate.cs` (new), `MainWindowViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`: the card beside the shortened LIFECYCLE bar, the confirmation.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 12 new keys in 12 languages.
- `docs/images/01-12-*.png`: retaken from v1.1.4.0 (the real window rendered at 1920x1080 against a demo service on a copy of the world).
- Tests: `scripts/Test-v1.1.4.0-Logic.ps1`; LogicHarness (4 scenarios), ArtworkHarness (2 checks: the card's fit in 12 languages at 950, 1100 and 1440 px, and its states).
