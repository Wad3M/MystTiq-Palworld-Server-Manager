<!-- MystTiq v1.0.5.0: file reviewed for this release (2026-10-06). -->
# v1.0.5.0 Changed Files

- `src/MystTiq.Core/Services/ModArchivePlanner.cs` (new): what a MOD archive holds and whether MystTiq installs it.
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: the ZIP install decides by the archive check (refusals
  logged), and installs a UE4SS MOD from its own folder.
- `src/MystTiq.Desktop/Services/ModSources.cs` (new): the sources (folders, Thunderstore, CurseForge, GitHub releases,
  Nexus Mods), the host-checked downloader.
- `src/MystTiq.Desktop/Services/NxmLinkHandoff.cs` (new): the nxm:// handoff, the opt-in per-user handler, the browser's
  folder and repository list.
- `src/MystTiq.Desktop/Program.cs`: a start with an nxm:// link hands it over.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ModBrowser.cs` (new), `Models/ModBrowserDtos.cs` (new), the MOD
  Library page's MOD Browser card, its Click handlers in `MainWindow.axaml.cs`, the Nexus download's archive check.
- `src/MystTiq.Desktop/Services/ButtonIntents.cs`: Search is an info (teal) button.
- 92 new texts in all 12 languages.
- `scripts/Test-v1.0.5.0-RouteSmoke.ps1` (new): the archive check through the service, with real repository archives.
- Tests: `scripts/Test-v1.0.5.0-Logic.ps1` (generated from the v1.0.4.0 gate, every earlier check carried), a LogicHarness
  scenario and ArtworkHarness checks with stand-in repositories.
