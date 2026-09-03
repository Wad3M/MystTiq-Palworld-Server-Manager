# v0.5.5.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.5.5.0 logic suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite (unchanged surface — this release touches no API routes).
5. Relaunch the Windows desktop on a native (non-DPI-scaled) display and visually verify: nav corner/outline is clean with no default-blue frame bleed, nav pill fills its full row height, hovering a selected nav item visibly brightens, the SERVER card shows red/amber/green correctly across states, OVERALL HEALTH label color matches its card, and the shared title/subtitle card renders correctly with no duplicate banner on at least Dashboard, Server Setup, and Backups.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion. This release touches no headless/API/platform-specific code, so the usual Linux runtime-verification pass does not apply the same way it does for backend features; a clean Linux desktop/headless build is sufficient evidence per release-notes/v0.5.5.0.md.
