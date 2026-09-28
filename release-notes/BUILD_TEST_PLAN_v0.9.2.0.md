<!-- MystTiq v0.9.4.0: file reviewed for this release (2026-09-28). -->
# v0.9.2.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Update-MystTiqUiText.ps1 -Check`: no hard-coded XAML text and no untranslated display binding left.
3. `scripts/Test-v0.9.2.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.1.0 gate (every earlier contract);
   - the v0.9.2.0 contracts: 12 name languages with English as the default and fallback, a cache per language,
     Japanese from the base tables, `?lang=` on the two routes, the Desktop sending its display language and reading
     the picker again on a change, sentence-by-sentence translation, the review stamp at v0.9.2.0;
   - the logic harness's per-language scenario (a stand-in extractor: separate caches, no re-read, English fallback);
   - the ArtworkHarness's composed-text checks in Japanese (map summary, Pal tooltip with coordinates, alpha, uptime,
     unknown sentences unchanged) and every template's round trip;
   - every carried smoke and the Linux VM checks.
4. Read the real server's names in Japanese and Korean with `extract_game_names.py --lang ja` / `--lang ko`.
5. Publish, launch, check `/healthz` reports 0.9.2.0; switch to Japanese, load the Give Item picker and see Japanese
   names, then switch back to English.
6. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
7. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.
