# v0.7.57.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.57.0 logic suite (`scripts/Test-v0.7.57.0-Logic.ps1 -RunBuild`), including the frozen v0.7.56.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness, AND (new this version) an actual native build via `scripts/Build-ConsoleProxy.ps1` plus an export-ordinal verification pass.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets (.NET solution — unaffected by this version, no C# changes).
4. New surface this release: `native/MystTiqConsoleProxy/` (dllmain.cpp, MystTiqConsoleProxy.def), `scripts/Build-ConsoleProxy.ps1`. Entirely outside the .NET solution — no `.csproj`, no reference from any C# project, nothing wired into the app's own routes/DTOs/install flow.
5. This release's native component was verified by actually building it and loading it in isolation (LoadLibrary/GetProcAddress/FreeLibrary against a throwaway test directory) — confirmed the DLL resolves and forwards to the real system dsound.dll, its lifecycle log writes correctly, and it unloads cleanly. It has NOT been loaded into a real, running PalServer process — this machine's production PalServer currently can't launch cleanly at all (a separate, disclosed, unresolved environment issue investigated the same session, not caused by this work). Do not copy this DLL into a real server's binary directory until that's confirmed resolved and the proxy has been tested against an actual game launch.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes — including the new `native/` directory and its build script.

Any failure blocks promotion.
