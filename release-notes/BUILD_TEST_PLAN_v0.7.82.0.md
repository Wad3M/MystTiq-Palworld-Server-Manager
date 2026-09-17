# v0.7.82.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.82.0 logic suite (`scripts/Test-v0.7.82.0-Logic.ps1 -RunBuild`), including the frozen v0.7.81.0 checkpoint regression gate and the carried-forward smoke suites (including `Test-v0.7.81.0-RouteSmoke.ps1`).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs` (`UpdatePipAsync`, timeout-aware `RunProcessAsync`), `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` (pip-update route), `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` (`MaxProjectableDays` crash fix), `src/MystTiq.Desktop/Models/ComponentVersionDto.cs` (`CanUpdateInPlace`/`SourceUrl`/`ComponentUpdateResultDto`), `src/MystTiq.Desktop/Models/PalworldConfigurationDtos.cs` (`PalworldSimpleToggleItem.IsDirty`), `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (`UpdatePipCommand`, `ServerSetupTableMaxHeight`, Dashboard STARTING-state fix, `RaiseIsBusyDependents` fix), `src/MystTiq.Desktop/MainWindow.axaml`/`.axaml.cs` (Update Center buttons, table resize, Configuration reorder + dirty highlighting), `src/MystTiq.Desktop/Styles/DesignSystem.axaml` (dirty-state styles).
5. **Live-verified** against the real desktop app: pip's Update button, the Unknown rows' Open buttons, the Server Setup table's live resize, Activity & Audit no longer flooding with the Alert Center warning, Configuration page's reordered layout with amber dirty highlighting, Dashboard's accurate "Starting / Not Ready" state, and the Generate button enabling correctly. User-confirmed final sign-off.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
