# v0.7.33.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.33.0 logic suite (`scripts/Test-v0.7.33.0-Logic.ps1 -RunBuild`), including the frozen v0.7.32.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Small XAML change this release (Dashboard grid/card edits only) plus a set of `MainWindowViewModel.cs` call-site edits (log-line request counts) — no AVLN1001 recurrence expected given the scope, but watch for it regardless per the recurring pattern.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
