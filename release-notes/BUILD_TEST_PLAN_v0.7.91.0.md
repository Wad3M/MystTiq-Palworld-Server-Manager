# v0.7.91.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.91.0 logic suite (`scripts/Test-v0.7.91.0-Logic.ps1 -RunBuild`), including the frozen v0.7.90.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs` (`BuildCrashRiskFindings`). No Desktop changes — the finding surfaces through the existing generic `DiagnosticFindings` binding.
5. Live-verify post-rebuild: query the diagnostics/status-polling route against the real running Default Server and confirm the new `configuration-crash-risk` finding appears with the correct state for its actual `BuildObjectDeteriorationDamageRate` value.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
