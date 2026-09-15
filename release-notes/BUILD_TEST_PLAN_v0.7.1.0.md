# v0.7.1.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.1.0 logic suite (`scripts/Test-v0.7.1.0-Logic.ps1 -RunBuild`), including the frozen v0.7.0.1 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): the 7 reordered pages read in the new order; the Pal Editor appears on Players, not Guilds; the Simple Settings gap note appears only when a curated setting is genuinely missing from the loaded INI; each tab shows Local/Remote under the server name; the "+" button renders a plain plus.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
