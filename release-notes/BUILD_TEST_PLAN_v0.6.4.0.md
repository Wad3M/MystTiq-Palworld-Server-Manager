# v0.6.4.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.4.0 logic suite (`scripts/Test-v0.6.4.0-Logic.ps1 -RunBuild`), including the frozen v0.6.3.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config): fetch `GET /api/v1/diagnostics/report` and confirm SteamCMD/PalServer-executable/BackupRoot each appear exactly once (not duplicated with a different label). Delete the backup root, recheck, confirm it becomes a fixable Warning; call the fix route and confirm the directory is recreated; recheck again and confirm it returns to Pass. Call fix on a non-fixable finding (e.g. UE4SS) and confirm it returns the real "BACKEND REQUIRED" reason, not a silent success.
5. On the Desktop app: open Doctor and confirm the unified findings list renders with working Recheck/Fix Automatically buttons; confirm the Dashboard's Overall Health badge changes when a real Fail/Warning finding exists, and shows STOPPED (not a health-derived label) when the server is intentionally stopped with nothing else wrong. On Diagnostics Center, point a connection profile at a deliberately wrong host/port/certificate and confirm "Diagnose Connection" reports the specific failing stage (DNS/TCP/TLS/HTTP) rather than one generic message; confirm all stages pass against the real working local instance.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
