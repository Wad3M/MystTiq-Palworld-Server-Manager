# v0.8.13.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.13.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.12.0 gate;
   - every carried smoke;
   - the game names smoke (a synthetic pak; needs Python on PATH);
   - the logic harness, including 2 "Game names" scenarios;
   - the ArtworkHarness, which checks every page's Ribbon for images.
4. Publish the desktop build and check `/healthz` reports 0.8.13.0. Confirm `headless\Tools\extract_game_names.py` is
   in the publish output.
5. Live check on a safe profile (the clone, which has its own real pak):
   - the Ribbon shows all new images;
   - the map's Pals list shows names;
   - the Give Item picker's Load IDs lists names and the game-only rows, and searching "lamball" finds SheepBall.
6. Linux: run `scripts/Test-v0.8.13.0-LinuxIsolated.ps1 -LinuxHost <VM address>`. It must end with "exit=0".
7. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
