# v0.8.21.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.21.0.
- `src/MystTiq.Core/Services/LinuxSystemdServiceManager.cs`: `BuildUnitText` (public, static) with `LimitNICE=-11`.
- `src/MystTiq.Core/Services/ProcessResourceControl.cs`: the Linux refusal names the fix (reinstall the service).
- `src/MystTiq.HeadlessHost/Program.cs`: the `service-unit` command (prints the unit; Linux), the `--fleet-root` option, and their usage lines.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.Host.cs`: the HOST tab's Linux note.
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`: the unit scenario;
  - new: `scripts/Test-v0.8.21.0-LinuxIsolated.ps1` and `.sh`, `scripts/Test-v0.8.21.0-RouteSmoke.ps1`, `scripts/Test-v0.8.21.0-Logic.ps1`;
  - 19 older smokes pass `--fleet-root` (v0.5.1.5 RuntimeSmoke; v0.7.12.0, 15.0, 64.0, 81.0, 97.0, 98.0, 100.0 to 104.0,
    107.0, 108.0, 110.0 to 113.0 RouteSmoke; v0.7.17.0 RemoteEnableSmoke).
- Docs:
  - new: `docs/architecture/v0.8.21.0-linux-priority.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
