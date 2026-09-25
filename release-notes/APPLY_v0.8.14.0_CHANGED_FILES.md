# v0.8.14.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.14.0.
- `src/MystTiq.Desktop/Services/RoleAccess.cs` (new): the Desktop's role rule.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - `CanOperate`, and `CanManageAdmin`/`CanManagePrincipals` on `RoleAccess`;
  - `RoleHiddenNotice` and `HasRoleHiddenNotice`;
  - these are raised on a principal change.
- `src/MystTiq.Desktop/MainWindow.axaml`:
  - role visibility on the gated cards;
  - Fleet Actions at Operator;
  - the Give box and finder gated;
  - the notice at the top of the page content.
- `scripts/Testing/MystTiq.LogicHarness`: links `RoleAccess.cs`, and adds the "Role cards" scenario.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: every gated card for each role, the notice, and an Operator render.
- New: `scripts/Test-v0.8.14.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.14.0-role-cards.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
