# v0.8.19.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.19.0.
- `src/MystTiq.HeadlessHost/RbacEndpointExtensions.cs`: `RequiredRoleMetadata`, `RequireRole` records its role,
  `DefaultMinimum`, and `RequireRoleByDefault` for a route group.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`:
  - both server route groups get the default;
  - explicit Operator roles on the everyday changes;
  - explicit Viewer on the fleet-level reads.
- `src/MystTiq.Desktop/`:
  - new: `ViewModels/MainWindowViewModel.RibbonRoles.cs`;
  - `Models/RibbonActionViewModel.cs`: `RequiredRole`, `RoleAllowed`, `ToolTipText`;
  - `ViewModels/MainWindowViewModel.cs`: gated Ribbon, rebuilt on a role change;
  - `MainWindow.axaml`: Ribbon buttons and the kick, ban, unban, teleport, RCON and save-now buttons follow the role.
- Tests:
  - `scripts/Testing/MystTiq.LogicHarness/Program.cs`, `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`;
  - new: `scripts/Test-v0.8.19.0-RouteSmoke.ps1`, `scripts/Test-v0.8.19.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.19.0-route-roles.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
