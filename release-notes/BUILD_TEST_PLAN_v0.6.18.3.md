# v0.6.18.3 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.18.3 logic suite (`scripts/Test-v0.6.18.3-Logic.ps1 -RunBuild`), including the frozen v0.6.18.2 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0):
   - Server Name/Description/Admin Password/Server Password display without quotes; the real `Value` (with quotes re-added) still gets written on Save Changes.
   - Selecting a QoL preset applies it immediately; typing a name and clicking "Save As Preset" adds it to the dropdown and persists it to `config-presets.json`.
   - Advanced Settings rows differing from default show the highlight style; it updates live as a value is edited.
   - Simple Settings shows all three new sub-groups (Network/Access/Limits, Gameplay Rates) alongside the existing Server Identity section.
   - Search/category filtering affects both Simple and Advanced views.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
