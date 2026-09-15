# Apply v0.7.24.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.24.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.24.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is documentation-only: `docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md` (two row corrections), plus the standard version-bump/changelog/architecture/release-notes set. No product code changed in `MystTiq.Core`, `MystTiq.HeadlessHost`, or `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
