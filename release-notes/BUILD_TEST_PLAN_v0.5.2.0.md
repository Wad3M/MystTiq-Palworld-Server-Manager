# v0.5.2.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.5.2.0 logic suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. Deploy to the reference Linux host via `scripts/Deploy-Test-MystTiqLinux.ps1` and run the v0.5.2.0 Linux acceptance suite.
6. Exercise Claim Orphaned Guild, Transfer Leadership, and Add Player to Guild against a disposable copy of a real save (never the live save); independently re-decode the result rather than trusting the service's own report.
7. Relaunch the Windows desktop and visually verify the unified Guild Ownership Operations card (operation-type selector, preview, confirm, apply) on the Guilds page.
8. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
