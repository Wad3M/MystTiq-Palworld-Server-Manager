# Apply v0.7.57.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.57.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.57.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. No `.NET` solution changes at all — `MystTiq.Core`, `MystTiq.HeadlessHost`, and `MystTiq.Desktop` are untouched. Adds a new, standalone native C++ artifact: `native/MystTiqConsoleProxy/` (dllmain.cpp, MystTiqConsoleProxy.def) plus its build script, `scripts/Build-ConsoleProxy.ps1`. Requires the MSVC v143 toolchain to build; not referenced by any part of the running application.

`MystTiqConsoleProxy.dll` has been verified in isolation only (loaded via LoadLibrary against a throwaway test directory) — it has NOT been tested against a real, running PalServer process. Do not deploy it to a live server's binary directory.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
