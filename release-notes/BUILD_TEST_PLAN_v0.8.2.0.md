# v0.8.2.0 Build and Test Plan

1. Clean, then PUBLISH the desktop build. Pipe the output through `Select-Object -Last`, never `-First`.
   - Clean covers bin/obj for both harnesses and for `scripts/Testing/FakePalServer`.
2. `scripts/Validate-Release.ps1 -Strict` after Clean: 0 errors / 0 warnings.
3. `scripts/Test-v0.8.2.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.1.0 gate;
   - every carried smoke;
   - the new service-mode smoke (`Test-v0.8.2.0-RouteSmoke.ps1`);
   - the logic harness;
   - `MystTiq.ArtworkHarness`.
4. The service-mode smoke runs the published `mysttiq-server.exe service-run` in the foreground. It uses its own
   FleetRoot and ports, and a stand-in PalServer on UDP 18482. It never touches an installed service or a real
   server.
5. Live checks:
   - the Desktop starts and `/healthz` answers;
   - the Default Server is untouched.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
