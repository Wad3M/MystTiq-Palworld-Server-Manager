# v0.8.12.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.12.0.
- `src/MystTiq.Core/Services/NatTopology.cs` (new): `Classify`, `ClassifyRoute` (with `routerRefused`),
  `IsCarrierGrade`, `IsPrivate`.
- `src/MystTiq.Core/Services/WanReachabilityService.cs`:
  - the router's WAN address, via UPnP GetExternalIPAddress, asked twice if refused;
  - the second-NAT check, with the first-hops route fallback;
  - the "Outside-in test" entry;
  - `ResolveControlUrl`, which only treats http(s) URLs as absolute (fixes UPnP on Linux);
  - the SSDP search from every LAN interface (fixes UPnP on multi-interface Windows);
  - default-payload pings and `TimeExceeded` (Linux).
- `src/MystTiq.Core/Models/WanReachabilityModels.cs`: optional `RouterWanIPv4`.
- `src/MystTiq.Desktop/Models/NetworkDiagnosticDtos.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`: the
  router's internet address, and the WAN section intro.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: the "Second NAT:" scenario.
- `scripts/Validate-Release.ps1`: `Test-v*-LinuxIsolated.ps1` is excluded from the stale-version scan, like the other
  version-specific test scripts.
- New scripts:
  - `scripts/Test-v0.8.12.0-RouteSmoke.ps1`
  - `scripts/Test-v0.8.12.0-Logic.ps1`
  - `scripts/Test-v0.8.12.0-LinuxIsolated.ps1` and `scripts/Test-v0.8.12.0-LinuxIsolated.sh`
- Docs:
  - new: `docs/architecture/v0.8.12.0-second-nat.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
