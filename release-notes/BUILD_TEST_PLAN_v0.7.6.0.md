# v0.7.6.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.6.0 logic suite (`scripts/Test-v0.7.6.0-Logic.ps1 -RunBuild`), including the frozen v0.7.5.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): opening two tabs against two different profiles, switching between them, confirms the player list/logs/dashboard update promptly rather than showing the previous tab's data; the Set Up New Server wizard's step is independent per tab; editing a profile updates any other open tab using it; deleting a profile in use by another tab is blocked with a clear message.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
