# v0.8.23.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.23.0.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - `IRoleGatedCommand`; the three command classes check the role gate;
  - the gates are installed at the end of the constructor and re-evaluated when the signed-in role changes.
- New: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.CommandRoles.cs`, the command → role table.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.RibbonRoles.cs`: the Ribbon uses the table too; Owner is ranked.
- `src/MystTiq.Desktop/MainWindow.axaml`: the 12 code-behind world-edit, mod ZIP and archive controls bind
  `CanManageAdmin`.
- Tests:
  - new: `scripts/Testing/Get-MystTiqCommandRoles.ps1` (the audit), `scripts/Test-v0.8.23.0-Logic.ps1`;
  - `scripts/Testing/MystTiq.ArtworkHarness/Program.cs` and `scripts/Testing/MystTiq.RemoteSignInHarness/Program.cs`:
    every gated command, and every page's controls, for each role.
- Docs:
  - new: `docs/architecture/v0.8.23.0-every-button-follows-the-role.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.