# v0.7.77.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.77.0 logic suite (`scripts/Test-v0.7.77.0-Logic.ps1 -RunBuild`), including the frozen v0.7.76.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs` (`DescribeWorkshopItem` runtime-detection fix, `TryDetectVersionFromLocalWorkshopHash`, `ResolveActiveUe4ssDllPath`, `RepairModAsync`), `LocalManagementApiHost.cs` (new repair route), `src/MystTiq.Desktop/Services/MystTiqApiClient.cs`/`IMystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`.
5. **Live-verified against the real production install directly** (no isolated clone needed — nothing here touches player save data): confirmed the Workshop-runtime item now reports installed; confirmed UE4SS version reports the real hash-matched value; confirmed PalSchema/QualityOfLife import via the real API (`mods.txt` now lists 8); confirmed the Repair flow's success path (re-ran repair on the freshly-imported PalSchema, survived cleanly) and its honest-failure path (attempted repair on AdminCommands, which has no known Workshop source — confirmed a clear failure message and confirmed AdminCommands' own files were completely untouched).
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
