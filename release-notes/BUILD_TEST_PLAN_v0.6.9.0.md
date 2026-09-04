# v0.6.9.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.9.0 logic suite (`scripts/Test-v0.6.9.0-Logic.ps1 -RunBuild`), including the frozen v0.6.8.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config):
   - Create an `IdleEmpty` automation rule via `POST /api/v1/automation/rules`; confirm it persists with `nextDueUtc: null`.
   - Disable and re-enable it; confirm `nextDueUtc` stays `null` throughout (never swept into the fixed-schedule poller).
   - Let several automation ticks run with the server stopped; confirm `idleSinceUtc` stays `null` and no errors appear in the log.
5. On a real running PalServer instance (when available — this session could not stand one up): confirm the server auto-stops after the configured idle threshold with 0 players online, that RCON warning broadcasts fire at the configured countdown offsets, and that a player joining during the countdown correctly aborts the stop.
6. On the Desktop app: confirm "IdleEmpty" appears in the Automation page's trigger-kind selector with a working threshold-minutes field.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
