# v0.5.1.5 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.5.1.5 logic suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. Relaunch the Windows desktop and visually verify the selected server tab/add control, Setup detail, Configuration Simple/Advanced views, Backup command row, timestamp-first Console, and Workspace summary/location controls.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
