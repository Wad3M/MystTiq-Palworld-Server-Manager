# v0.8.20.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.20.0.
- `src/MystTiq.HeadlessHost/`:
  - new: `HeadlessHostHistoryService.cs` (the service, `HostHistoryMath`, the records);
  - `LocalManagementApiHost.cs`: created with the fleet folder, started and stopped with the host, `/host/history`;
  - `ServerProfileHost.cs`: `HostHistory`.
- `src/MystTiq.Desktop/`:
  - new: `Controls/HostHistoryChart.cs`, `ViewModels/MainWindowViewModel.HostHistory.cs`;
  - `Models/HostDtos.cs`, `Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.Host.cs`;
  - `MainWindow.axaml`: the History card.
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`, `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`;
  - new: `scripts/Test-v0.8.20.0-RouteSmoke.ps1`, `scripts/Test-v0.8.20.0-LinuxIsolated.ps1` and `.sh`,
    `scripts/Test-v0.8.20.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.20.0-host-history.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
