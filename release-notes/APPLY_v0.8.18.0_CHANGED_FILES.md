# v0.8.18.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.18.0.
- `src/MystTiq.Core/Services/ServerNetworkPolicy.cs` (new): the policy, the game's defaults, the planner, the
  Engine.ini text edit and the write-before-start.
- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs`, `LinuxServerLifecycleService.cs`: write the limits just
  before launching, and log it.
- `src/MystTiq.HeadlessHost/`:
  - new: `HeadlessNetworkPolicyService.cs`;
  - `LocalManagementApiHost.cs`, `ServerProfileHost.cs`, `HeadlessHostMonitor.cs`: wiring, `/network/policy`, and
    bandwidth in `/host`.
- `src/MystTiq.Desktop/`:
  - new: `ViewModels/MainWindowViewModel.Bandwidth.cs`;
  - `Models/HostDtos.cs`, `Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.Host.cs`;
  - `MainWindow.axaml`: the Bandwidth limits card.
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`, `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`;
  - new: `scripts/Test-v0.8.18.0-RouteSmoke.ps1`, `scripts/Test-v0.8.18.0-LinuxIsolated.ps1` and `.sh`,
    `scripts/Test-v0.8.18.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.18.0-bandwidth.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
