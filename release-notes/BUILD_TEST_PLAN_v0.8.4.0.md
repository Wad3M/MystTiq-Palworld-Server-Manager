# v0.8.4.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer. Then PUBLISH the desktop
   build. Use `Select-Object -Last` on its output, never `-First`.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.4.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.3.0 gate;
   - every carried smoke;
   - the new delivery-pause smoke (`Test-v0.8.4.0-RouteSmoke.ps1`);
   - the logic harness;
   - `MystTiq.ArtworkHarness`.
4. The delivery smoke runs its own webhook receiver on localhost, with its own FleetRoot and ports. Nothing is sent off
   the machine.
5. Live check, on the clone profile only (it has no outside channels):
   - Alert Center shows the new card;
   - Pause 1 hour sets the pause on the host;
   - Resume clears it;
   - the Default Server is untouched.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
