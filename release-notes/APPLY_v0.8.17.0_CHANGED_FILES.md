# v0.8.17.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.17.0.
- `src/MystTiq.Core/Services/` (new):
  - `ServerResourcePolicy.cs`: the policy and its pure rules;
  - `ProcessResourceControl.cs`: Windows priority/EcoQoS, Linux per-thread niceness;
  - `HostMetricsReader.cs`: processor, memory and processor name.
- `src/MystTiq.HeadlessHost/`:
  - new: `HeadlessHostMonitor.cs`, `HeadlessResourcePolicyService.cs`;
  - `ServerProfileHost.cs`, `LocalManagementApiHost.cs`: wiring, and the routes `/host` and `/resources/policy`;
  - `HeadlessAutomationService.cs`: the policy on every tick.
- `src/MystTiq.Desktop/`:
  - new: `Models/HostDtos.cs`, `ViewModels/MainWindowViewModel.Host.cs`;
  - `Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs`: `GetHostAsync`, `SaveResourcePolicyAsync`;
  - `ViewModels/MainWindowViewModel.cs`: partial; page flags, navigation, Ribbon, busy states, auto-refresh;
  - `Models/NavigationPage.cs`: `Host`, appended;
  - `Services/ArtworkCatalog.cs`: Host uses the System art;
  - `MainWindow.axaml`: the Host category tab, the navigation item and the page;
  - `Styles/DesignSystem.axaml`: the Host tab's accent;
  - `Assets/i18n/en.json`, `de.json`, `es.json`: category, navigation, header and Ribbon group.
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`: two scenarios and `FakeResourceControl`;
  - `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: the Host page, the role table entry, 8 category tabs;
  - new: `scripts/Test-v0.8.17.0-RouteSmoke.ps1`, `scripts/Test-v0.8.17.0-LinuxIsolated.ps1` and `.sh`,
    `scripts/Test-v0.8.17.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.17.0-host-tab-and-priority.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
