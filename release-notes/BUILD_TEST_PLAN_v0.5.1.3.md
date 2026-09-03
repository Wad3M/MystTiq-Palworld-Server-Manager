# v0.5.1.3 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.5.1.3 logic suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. Relaunch the Windows desktop and visually verify Dashboard-only Home, System Notifications, the top add-server flow, UE4SS version/fork information, input sizing, and the compact glass selected state.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
