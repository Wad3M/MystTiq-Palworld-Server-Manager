# v0.7.71.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.71.0 logic suite (`scripts/Test-v0.7.71.0-Logic.ps1 -RunBuild`), including the frozen v0.7.70.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessMonitoringService.cs` (`ResolveConsoleSources` gains a `"PalDefender log"` source, newest-file-in-`PalDefender\Logs`-directory pattern matching the existing AdminCommands source).
5. Verified live, not just statically: rebuilt via `Build.ps1 DesktopWindows`, relaunched the real local API host, confirmed `/api/v1/servers/default/logs/tail` and `/api/v1/servers/second-local/logs/tail` both report the new source and real PalDefender content in the merged tail.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
