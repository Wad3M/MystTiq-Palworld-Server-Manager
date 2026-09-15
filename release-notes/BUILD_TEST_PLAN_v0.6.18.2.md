# v0.6.18.2 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.18.2 logic suite (`scripts/Test-v0.6.18.2-Logic.ps1 -RunBuild`), including the frozen v0.6.18.1 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0):
   - Server Setup page no longer has a hero card, First-Run Server Defaults card, or Check-for-Updates/Install-Missing buttons.
   - The stat row now has 4 items including environment health + a Verify Files action.
   - The Settings page's connection-profile editor has a new "In-Game Server Defaults (new server only)" card, bound to the same `Setup*` fields and `CreateDefaultServerSettingsCommand` as before, gated by `IsCreatingNewProfile`.
   - `IsCreatingNewProfile` correctly flips when `SelectedProfile` transitions to/from null (confirmed by reading the corrected `SelectedProfile` setter, which now raises the notification before its null-guard, not after).
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
