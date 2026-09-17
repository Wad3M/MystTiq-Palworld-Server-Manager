# Apply v0.7.72.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.72.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.72.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `native/MystTiqConsoleProxy/dllmain.cpp` (the console-write hook, replacing v0.7.57.0's disclosed no-op scaffold), `src/MystTiq.HeadlessHost` (new `HeadlessConsoleCaptureProxyService.cs`, `ServerProfileHost.cs` and `LocalManagementApiHost.cs` wiring, three new routes, and `HeadlessMonitoringService.cs`'s console-source list), and `scripts/Build-AvaloniaDesktop.ps1`/`scripts/Build-WindowsHeadless.ps1` (best-effort staging of the native proxy DLL alongside the headless publish output). No change to `MystTiq.Core` or `MystTiq.Desktop`.

The native proxy DLL itself must be rebuilt separately via `scripts/Build-ConsoleProxy.ps1` (MSVC toolchain required) before `POST /server/console-capture/install` has anything to install — a fresh checkout without that build step still runs normally, just without console-capture install available until it's run.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
