# v0.7.5.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.5.0 logic suite (`scripts/Test-v0.7.5.0-Logic.ps1 -RunBuild`), including the frozen v0.7.4.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): the Settings page no longer shows Managed Server Configuration/Security cards while creating a new profile; the six new collapsible sections (World Map, Pal Editor, WAN Reachability, Local Machine Diagnostics, Discord Bot, Anti-Cheat) render collapsed on first load and expand/collapse correctly on click; overall pages read as slightly denser without looking cramped.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
