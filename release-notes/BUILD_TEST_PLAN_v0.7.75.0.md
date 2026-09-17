# v0.7.75.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.75.0 logic suite (`scripts/Test-v0.7.75.0-Logic.ps1 -RunBuild`), including the frozen v0.7.74.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessPlayerDeletionService.cs`, `HeadlessPlayerCopyService.cs`, `HeadlessPlayerRegistryService.cs` (`Forget`), `ServerProfileHost.cs`/`LocalManagementApiHost.cs` (4 new routes), `src/MystTiq.Desktop/Models/PlayerOperationDtos.cs`, `Services/MystTiqApiClient.cs`/`IMystTiqApiClient.cs`, `Views/ConfirmDeletePlayerDialog.*`, `Views/ConfirmCopyPlayerDialog.*`, `Views/SelectPlayerDialog.*`, `MainWindow.axaml`/`.axaml.cs`, `ViewModels/MainWindowViewModel.cs`, `Styles/DesignSystem.axaml` (`MenuItem:disabled`).
5. **Live-verified on an isolated clone of the real production save (5 real players, 2 real guilds) — never the real production server**: both Delete Player and Copy Player previewed and applied successfully via the real API against real save data. A real bug (operation-lock-release ordering) was found, fixed, and the fix re-verified. Every result was independently re-checked by decoding the affected `.sav`/`Level.sav` a second, separate way outside the app (not just trusting the app's own success response) — confirmed byte-correct: the deleted player's save is genuinely gone and their guild reference genuinely removed from the real `players` array; the copy destination's `PlayerUId` stayed genuinely its own while the copied fields matched the source exactly.
6. `dotnet build src/MystTiq.Desktop/MystTiq.Desktop.csproj` clean. No live GUI click-testing of the new dialogs was possible in this environment (no GUI-automation capability) — code-reviewed against the same proven dialog shape already working in production.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
