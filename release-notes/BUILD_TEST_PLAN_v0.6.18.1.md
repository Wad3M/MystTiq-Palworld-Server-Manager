# v0.6.18.1 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.18.1 logic suite (`scripts/Test-v0.6.18.1-Logic.ps1 -RunBuild`), including the frozen v0.6.18.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm the three fixes directly:
   - `HeadlessPalEditService.GenderMatches` exists and does an exact-suffix comparison, not `EndsWith`.
   - `HeadlessAntiCheatService.RecordAsync` accepts a `cooldownIdentity` parameter, and the Pal-scan call site passes `pal.InstanceId`.
   - `HeadlessDiscordBotService`'s 401 detector requires `isWarningOrWorse` and `message.Source == "Gateway"` before counting toward the failure threshold.
5. Confirm no regression: the v0.6.18.0 Anti-Cheat/Discord Bot/Pal Editor static contracts and the frozen v0.6.18.0 checkpoint's own logic gate still pass unchanged.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
