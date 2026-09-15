# v0.7.11.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.11.0 logic suite (`scripts/Test-v0.7.11.0-Logic.ps1 -RunBuild`), including the frozen v0.7.10.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live multi-server check: started two full sidecar instances at once on different ports against separate isolated server roots. Both stayed healthy simultaneously under distinct PIDs; each independently created a backup and each instance's backup list contained only its own file — zero cross-contamination.
5. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): pressing the window's close button with no server running exits the app fully; with a server running it minimizes to tray and shows the reminder toast; closing a running tab shows the Stop & Close/Leave Running/Cancel dialog; launching a second instance while one is already running shows the "already running" notice and does not open a second window.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
