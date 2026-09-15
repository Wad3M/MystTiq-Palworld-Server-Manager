# Apply v0.7.48.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.48.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.48.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.Desktop` (`Services/ThemeColorMath.cs` — new; `Services/ThemeCatalog.cs`/`Services/ThemeApplier.cs` — new derived-color-family logic and Inspect/Target gradient coverage; `MainWindow.axaml` and `Styles/DesignSystem.axaml` — extensive hardcoded-color-to-DynamicResource conversions) and `MystTiq.HeadlessHost` (`HeadlessComponentUpdateService.cs` — UE4SS staleness fix, unrelated to the theming work, folded in per direct request). No change to `MystTiq.Core`.

This release changes the app's default visual appearance slightly: the newly-computed theme-derived colors (page-accent borders/glows, tinted card gradients) replace the previous hand-tuned static values immediately on startup, for every theme including the default one — see the architecture doc for why this tradeoff was made. No functional/behavioral change, purely visual.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
