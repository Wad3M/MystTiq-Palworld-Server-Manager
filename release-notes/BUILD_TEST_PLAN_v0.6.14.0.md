# v0.6.14.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.14.0 logic suite (`scripts/Test-v0.6.14.0-Logic.ps1 -RunBuild`), including the frozen v0.6.13.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Console live-refresh fix, against a real isolated instance with real MOD/UE4SS setup and a real historical `AdminCommands` log present (both needed to reproduce the merge-starvation bug):
   - Start the real PalServer process; poll `GET /api/v1/logs/tail?lines=10` repeatedly during the operation and confirm fresh MystTiq lifecycle/stdout content (not just historical log content) appears, growing in real time.
   - Confirm a `[MYSTTIQ] PalServer ready...` line appears in the tail the moment the server actually becomes ready, not only after the whole operation returns.
   - Stop the server and confirm the equivalent shutdown narrative appears live during that operation too.
5. On the Desktop app, if a click-through session is available: watch the Console page during a real Start/Stop/Restart and confirm it visibly updates throughout the operation, not just once at the end.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
