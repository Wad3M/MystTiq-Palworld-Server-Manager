# v0.7.62.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.62.0 logic suite
   (`scripts/Test-v0.7.62.0-Logic.ps1 -RunBuild`), including the frozen v0.7.61.0 checkpoint
   regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/
   v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: a single `MaxWidth="620"` attribute added to the page-header `Border`
   in `MainWindow.axaml`. No new routes, no DTO/schema changes, no MystTiq.Core/HeadlessHost changes.
5. **Verified live, not just via the static build gate**: this fix was validated by reproducing the
   actual reported bug (ribbon/category tabs rendering blank after a reconnect) on a live running
   instance, confirming via non-invasive `cdb`+SOS process inspection that the underlying data was
   correct throughout (ruling out a data/logic bug), applying the fix, rebuilding, and confirming
   live on both the Dashboard and Inspector pages that the same reconnect sequence no longer
   reproduces the bug. This is real behavioral verification beyond what the automated test script
   alone can check (a static XAML file, by itself, can't detect a runtime Arrange-pass layout bug).
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
