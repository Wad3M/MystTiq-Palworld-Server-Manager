# v0.7.46.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.46.0 logic suite (`scripts/Test-v0.7.46.0-Logic.ps1 -RunBuild`), including the frozen v0.7.45.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Only change this release: `MainWindowViewModel.ApplyConsoleFilter` now builds `FilteredLogLines` from `LogLines.Reverse()`. No route, DTO, or server-side contract changed — this is Desktop-only.
5. The `-port=8211` config fix and the console-capture root cause were both diagnosed/applied live against the user's real running server this session, outside the normal build/test pipeline — no code change accompanies the port fix (it was a live configuration write via the existing `/config/editable` route, not a code defect), and the console-capture fix is deliberately not included in this version (tracked separately).

Any failure blocks promotion.
