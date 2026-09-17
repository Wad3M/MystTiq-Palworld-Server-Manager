# v0.7.83.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.83.0 logic suite (`scripts/Test-v0.7.83.0-Logic.ps1 -RunBuild`), including the frozen v0.7.82.0 checkpoint regression gate and the carried-forward smoke suites (including `Test-v0.7.81.0-RouteSmoke.ps1`).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (wizard step renumbering, Install Directory provisioning, restart-and-reconnect orchestration, `BuildProfileFromEditor` collision-guard fix, `WizardStep1HeaderText`, `SetupServerName`/`ProfileName` sync), `src/MystTiq.Desktop/Models/FleetDtos.cs` (`AddFleetProfileRequestDto`/`AddFleetProfileResultDto`), `src/MystTiq.Desktop/Services/MystTiqApiClient.cs`/`IMystTiqApiClient.cs` (`AddFleetProfileAsync`), `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs` (`RestartOwnedSidecarAsync`), `src/MystTiq.Desktop/MainWindow.axaml`/`.axaml.cs`, `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` (round-2 crash fix).
5. **Live-verified**: the full second-server flow (register → restart → reconnect → install → default settings) was driven directly against the real REST API end-to-end, creating a genuine new server ("MystTiqClaude") alongside the existing Default Server and Palworld Server 2 (Clone), with the pre-existing Default Server's own settings confirmed untouched. The wizard's own click-path (Step 1 auto-connect, World Source reorder, Identity & Ports) was live-verified directly in the desktop UI, where the duplicate-connection-guard and Step 1 header bugs were found and fixed.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
