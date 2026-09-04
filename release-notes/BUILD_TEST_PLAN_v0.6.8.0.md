# v0.6.8.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.8.0 logic suite (`scripts/Test-v0.6.8.0-Logic.ps1 -RunBuild`), including the frozen v0.6.7.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config):
   - Install a fake PAK MOD (v1 content); confirm an absent-marker snapshot was written. Install v2 content over it; confirm the snapshot now holds v1. Roll back; confirm v1's exact content is restored.
   - Install a brand-new MOD, then roll it back; confirm the file is deleted entirely (not left in place).
   - Attempt rollback of a MOD that was never installed or deleted; confirm an honest failure, not a silent success.
   - Delete an existing MOD, then roll back the delete; confirm it's restored.
   - Induce a real Misconfigured MOD (a `.ucas` file with no matching `.pak`/`.utoc`); confirm `GET /mods` reports `overallHealth: "Degraded"`.
   - Confirm `ModHealthDegraded` is enabled by default (`GET /alerts/rules`); poll `GET /notifications` and confirm a real alert appears naming the specific MOD and its health state.
5. On the Desktop app: confirm "Rollback Selected" appears on the MOD Dashboard and completes without errors; confirm the new MOD-health toggle appears on the Alert Center page.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
