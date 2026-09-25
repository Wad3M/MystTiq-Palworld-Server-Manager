# v0.8.9.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer. Then PUBLISH the desktop build. Use `Select-Object -Last` on its output, never `-First`.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.9.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.8.0 gate;
   - every carried smoke;
   - the new crash report smoke;
   - the logic harness, including 3 "Crash reports" scenarios;
   - `MystTiq.ArtworkHarness`.
4. Real-data check. Copy only the real `CrashContext.runtime-xml` files into an isolated instance, never the real
   server's MystTiq state, and run analyze. All three reports should be read and classified.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
