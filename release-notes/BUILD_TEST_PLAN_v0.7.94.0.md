# v0.7.94.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and `scripts/Test-v0.7.94.0-Logic.ps1 -RunBuild`, including the frozen v0.7.93.0 checkpoint regression gate, the carried-forward smoke suites, and `MystTiq.LogicHarness` (now including 9 kit scenarios).
3. New surface: `HeadlessKitService` (validation, commands, claims, auto-gift), kit routes, Starter Kits card.
4. Live check after the final rebuild: `GET /players/kits` on the real Default Server reports the provider status; write/validation/give routes on the scratch server, then cleared.
5. Not covered: PalDefender's live reply (needs the Default Server running with an online player).
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
