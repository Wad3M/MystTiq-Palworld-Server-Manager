# v0.6.18.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.18.0 logic suite (`scripts/Test-v0.6.18.0-Logic.ps1 -RunBuild`), including the frozen v0.6.17.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Anti-Cheat, against an isolated `mysttiq-server.exe` instance (never the production directory or process):
   - Confirm `GET /anticheat/rules` returns the safe (`Flag`-only, enabled) defaults on first run.
   - Using a synthetic-but-schema-accurate `/v1/api/players` response (one player with a malformed Steam ID, one with an impossible level): confirm `EvaluateLivePlayersAsync` detects both and fires a `Flag` finding (notification + activity entry), and that `GET /anticheat/findings` returns them.
   - Confirm the per-`(rule, player)` cooldown suppresses a repeat finding for the same condition within the cooldown window.
   - Switch a rule to `Kick`; confirm the moderation path is invoked (`PlayerModerationCoordinator.ExecuteAsync`) with the finding's detail as the reason.
   - Pal stat scan: run against real, isolated, production-derived save data; confirm `ListPalsAsync` is called read-only (`Level.sav` byte-for-byte unchanged after the scan) and any genuinely out-of-bounds Pal is correctly flagged.
5. On the Desktop app: confirm the Anti-Cheat card on the Alert Center page loads/saves rules and displays recent findings.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
