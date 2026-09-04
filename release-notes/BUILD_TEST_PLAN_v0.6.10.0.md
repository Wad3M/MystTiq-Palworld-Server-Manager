# v0.6.10.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.10.0 logic suite (`scripts/Test-v0.6.10.0-Logic.ps1 -RunBuild`), including the frozen v0.6.9.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance built from a full independent copy of a real Palworld installation (never the production directory or process):
   - `POST /server/clone` against a stopped source profile; confirm the new profile's `PalWorldSettings.ini` has correctly offset ports.
   - Restart MystTiq, confirm both profiles list via `GET /servers`.
   - Start both profiles' real PalServer processes; confirm via `Get-NetUDPEndpoint` (or platform equivalent) that both bind their correct, distinct ports — not the same port or a silent fallback.
   - Leave both running and poll status every ~20s for at least 5 minutes; confirm both stay `ready:true` / `crashDetected:false` throughout.
   - Create a real `IdleEmpty` automation rule with a short threshold on one profile; confirm it fires (warning countdown, final recheck, real stop) while the other profile is unaffected.
   - If a second machine or VM with a live MystTiq instance is available: make an authenticated HTTPS call from this machine to it and confirm a real response; confirm an unauthenticated call is correctly rejected.
5. On the Desktop app: confirm the Fleet page's Clone World card submits and reports a result.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
