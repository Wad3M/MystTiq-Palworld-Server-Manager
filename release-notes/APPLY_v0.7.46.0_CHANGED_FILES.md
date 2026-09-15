# Apply v0.7.46.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.46.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.46.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `ViewModels/MainWindowViewModel.cs` (`ApplyConsoleFilter` now reverses `LogLines` when building `FilteredLogLines`). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

If you applied the `-port=8211` launch-argument fix during this session's live troubleshooting, that change lives in your own `mysttiq.json` (`C:\ProgramData\MystTiqPalworldServer\mysttiq.json` by default), not in this source ZIP — it is not overwritten or affected by applying this update.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
