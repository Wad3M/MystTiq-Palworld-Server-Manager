# v0.6.7.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.7.0 logic suite (`scripts/Test-v0.6.7.0-Logic.ps1 -RunBuild`), including the frozen v0.6.6.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config), using a real copy of a save with one player's `.sav` deliberately removed to create a genuine broken guild-member reference:
   - `POST /api/v1/guilds/ownership/preview` with `operationType=remove-broken-member` for that guild/player; confirm `canApply:true` with the expected message. Confirm Preview rejects the same operation for a player who *does* have a valid save.
   - `POST /api/v1/guilds/ownership/apply` with the returned token; confirm `state:"Committed"` (retry once if a transient file-lock occurs — this pipeline is otherwise unchanged from the already-shipped Claim/Transfer operations).
   - With a mock or real REST endpoint reporting the broken player plus a second player sharing the same Steam ID: confirm `GET /api/v1/diagnostics/report` includes both a `"Steam ID Collision"` and a `"Missing Save File"` finding under category `"Identity"`, and confirm via the report's `warnings` count that neither is counted toward Overall Health.
5. On the Desktop app: confirm "Remove Broken Member" appears in the Guild Ownership Operations combo box and completes a full preview/apply cycle without errors.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
