# v0.7.70.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.70.0 logic suite (`scripts/Test-v0.7.70.0-Logic.ps1 -RunBuild`), including the frozen v0.7.69.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites and the expanded `scripts/Testing/MystTiq.LogicHarness` (now 21 scenarios).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessNotificationRoutingService.cs` (`NotificationChannelConfig` gains Email/SMTP fields, `DispatchEmailAsync` sends via `SmtpClient`), `scripts/Testing/MystTiq.LogicHarness/Program.cs` (1 new scenario with a real SMTP protocol stub server).
5. Verified with a real SMTP wire-protocol stub server (not a mock of the dispatch method) — envelope, authentication, subject and body all confirmed reaching the wire correctly. Not verified against a real mail provider (Gmail, Outlook, etc.) — disclosed explicitly.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
