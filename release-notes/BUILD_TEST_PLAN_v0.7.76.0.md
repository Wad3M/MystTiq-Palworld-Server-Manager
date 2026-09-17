# v0.7.76.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.76.0 logic suite (`scripts/Test-v0.7.76.0-Logic.ps1 -RunBuild`), including the frozen v0.7.75.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/Views/SelectGuildDialog.*`, `Views/ConfirmOperationDialog.*`, `MainWindow.axaml`/`.axaml.cs`, `ViewModels/MainWindowViewModel.cs`.
5. Verified: `dotnet build src/MystTiq.Desktop/MystTiq.Desktop.csproj` clean. No new server-side mutation logic this version — the underlying Base/Guild ownership operations were already live-verified against real save data in this project's earlier history; this version only adds new Desktop-side orchestration calling those same, unchanged endpoints. No live GUI click-testing was possible in this environment (no GUI-automation capability) — code-reviewed against the same dialog/ViewModel-method shape already proven for Player Delete/Copy (v0.7.75.0).
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
