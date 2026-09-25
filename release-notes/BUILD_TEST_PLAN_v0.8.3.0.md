# v0.8.3.0 Build and Test Plan

1. Clean, including the bin/obj folders of both harnesses and FakePalServer. Then PUBLISH the desktop build. Use
   `Select-Object -Last` on the output, never `-First`.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.3.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.2.0 gate;
   - every carried smoke, including the v0.8.2.0 service-mode smoke;
   - the new catalogue smoke (`Test-v0.8.3.0-RouteSmoke.ps1`);
   - the logic harness;
   - `MystTiq.ArtworkHarness`.
4. The catalogue smoke uses a synthetic save and its own FleetRoot and ports. It needs no real server data.
5. Live check on the clone profile, Players page → Player Administration:
   - Load IDs lists the world's ids;
   - search narrows the list;
   - Add to Give writes the right line;
   - selecting a Pal switches the box to Level.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
