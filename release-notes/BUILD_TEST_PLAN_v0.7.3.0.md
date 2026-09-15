# v0.7.3.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.3.0 logic suite (`scripts/Test-v0.7.3.0-Logic.ps1 -RunBuild`), including the frozen v0.7.2.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): clicking "Set Up New Server" starts at Step 1; Next only enables once connected; Step 2 shows the port warnings inline; Step 3's Save Profile ends the wizard and returns to normal profile editing; editing an existing saved profile shows no wizard chrome at all.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
