# v0.6.15.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.15.0 logic suite (`scripts/Test-v0.6.15.0-Logic.ps1 -RunBuild`), including the frozen v0.6.14.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Pal Editor, against a real isolated copy of production-derived save data (never the production directory or process):
   - `GET /pals` and confirm real Pal entries are returned with species/level/IVs matching direct inspection of the source save.
   - Confirm at least one genuinely-owned Pal (independently verified via the raw save data) resolves to its correct real owner name, not null.
   - Preview and apply a real edit (e.g. change Level, toggle the Lucky flag, set a nickname); confirm the preview's diff matches what was requested.
   - Independently re-fetch `GET /pals` after apply and confirm every changed field landed exactly as requested while every untouched field (Talents, owner, other Pals) is byte-identical to before.
   - Confirm the explorer sidecar (`Level.sav.json`) reflects the change immediately with no extra step.
   - Confirm Apply correctly refuses while PalServer is running, and correctly rejects a stale/reused preview token.
5. On the Desktop app: confirm the Guilds page's new Pal Editor card lists Pals, populates the edit form on selection, and the Preview/Apply flow renders correctly.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
