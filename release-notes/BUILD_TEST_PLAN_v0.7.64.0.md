# v0.7.64.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.64.0 logic suite (`scripts/Test-v0.7.64.0-Logic.ps1 -RunBuild`), including the frozen v0.7.63.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts, the new v0.7.64.0 route smoke script, and the expanded `scripts/Testing/MystTiq.LogicHarness` (now 17 scenarios).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Core/Services/ConsoleLogRotation.cs` (new), `src/MystTiq.HeadlessHost/HeadlessAutomationService.cs` (`ValidateTriggerAndAction`, called from `CreateRule`/`UpdateRule`), `src/MystTiq.HeadlessHost/HeadlessConsoleLogWriter.cs`, `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs` (both now call `ConsoleLogRotation.RotateIfNeeded`), `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` (automation POST/PUT routes catch `ArgumentException` → 400), `src/MystTiq.HeadlessHost/HeadlessEnvironmentChecklistService.cs` (UE4SS/Backup Storage row text and flags corrected), `src/MystTiq.Desktop/MainWindow.axaml` (Repair Center stale text corrected), `scripts/Testing/MystTiq.LogicHarness/Program.cs` (11 new scenarios), `scripts/Test-v0.7.64.0-RouteSmoke.ps1` (new).
5. This release's fixes were verified live against the real running headless sidecar (`scripts/Test-v0.7.64.0-RouteSmoke.ps1`), not just statically — the automation-validation and checklist-flag fixes are HTTP-level behavior, not GUI rendering, so this environment can verify them directly. The three text-only fixes and the Desktop-side UE4SS navigation unlock were NOT visually confirmed in the GUI — same disclosed limitation as every recent Desktop-touching release.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
