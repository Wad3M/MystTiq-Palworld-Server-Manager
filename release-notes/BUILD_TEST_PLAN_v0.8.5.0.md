# v0.8.5.0 Build and Test Plan

1. Clean, including the bin/obj folders of both harnesses and FakePalServer. Then PUBLISH the desktop build. Use
   `Select-Object -Last` on its output, never `-First`.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.5.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.4.0 gate;
   - every carried smoke;
   - the logic harness;
   - `MystTiq.ArtworkHarness`, including the display-language renders.

   The static contracts check that:
   - every `{services:Tr}` key exists in English;
   - German and Spanish match English's keys exactly;
   - English is unchanged.
4. Live check, in Settings → Appearance → Language:
   - Deutsch applies at once;
   - the app reopens in German;
   - choosing English switches back.

   Leave the real app on English.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
