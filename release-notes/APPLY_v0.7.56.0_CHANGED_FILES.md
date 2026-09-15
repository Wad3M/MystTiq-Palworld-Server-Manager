# Apply v0.7.56.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.56.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.56.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches only `MystTiq.Desktop` (`Services/ThemeApplier.cs`, `Styles/DesignSystem.axaml`). No change to `MystTiq.Core`, `MystTiq.HeadlessHost`, or any route/DTO.

Purely a visual-theming pass — no new outbound network calls, no new capabilities, no data migration.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
