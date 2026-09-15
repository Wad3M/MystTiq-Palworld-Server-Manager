# v0.7.67.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.67.0 logic suite (`scripts/Test-v0.7.67.0-Logic.ps1 -RunBuild`), including the frozen v0.7.66.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/Views/TrayReminderToast.axaml.cs` (`LayoutUpdated` subscription added alongside `Opened`).
5. **Verified with a real, exact pixel-coordinate measurement on the actual reported hardware** — a scratch standalone Avalonia harness (not part of the shipped solution) drove the real class on this machine's actual multi-monitor layout, confirmed via `System.Windows.Forms.Screen.AllScreens` to match the original bug report exactly. Before this fix: `(766, -834)` vs an expected `(1972, -127)`. After: exact match at `(1972, -127)`. Full before/after logs in the architecture doc.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
