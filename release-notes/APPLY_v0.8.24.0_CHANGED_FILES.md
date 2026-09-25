# v0.8.24.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.24.0.
- `src/MystTiq.Core/Services/ServerResourcePolicy.cs`: `Cores`, `CoreSelection`, `TargetAffinity`.
- `src/MystTiq.Core/Services/ProcessResourceControl.cs`: `GetAffinity`/`SetAffinity` (Windows mask; Linux
  `sched_setaffinity` on every thread).
- `src/MystTiq.HeadlessHost/HeadlessResourcePolicyService.cs`: applies the cores; each process's cores and the core
  count in the snapshot.
- New: `src/MystTiq.HeadlessHost/PolicyApplyingLifecycle.cs`, which applies the policy when a start or restart
  succeeds; wired in `LocalManagementApiHost.cs`.
- Desktop:
  - `src/MystTiq.Desktop/Models/HostDtos.cs`;
  - `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.Host.cs`;
  - `src/MystTiq.Desktop/MainWindow.axaml` (the Processor cores field).
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`: three scenarios; the fakes gain affinity, stop and restart;
  - `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: the priority card's new title in the role-card table;
  - new: `scripts/Test-v0.8.24.0-RouteSmoke.ps1`, `scripts/Test-v0.8.24.0-LinuxIsolated.ps1` and `.sh`,
    `scripts/Test-v0.8.24.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.24.0-processor-cores.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.