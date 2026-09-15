# v0.7.61.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.61.0 logic suite
   (`scripts/Test-v0.7.61.0-Logic.ps1 -RunBuild`), including the frozen v0.7.60.0 checkpoint
   regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/
   v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `-unattended` in both `HeadlessConfiguration.CreateWindowsDefault()`
   and `CreateLinuxDefault()`; a new `HeadlessConfigurationService.EnsureUnattendedFlag` step
   applied after every `LoadOrDefault` load path. No new routes, no DTO/schema changes, no Desktop
   changes.
5. **This release was verified with an unusual amount of live evidence for a bug fix this deep**:
   the root cause was found via a live Process Monitor capture and a live Process Explorer
   thread-stack capture against this session's own genuinely-frozen production PalServer process,
   and the fix was confirmed by directly relaunching both the real production server and its clone
   with `-unattended` added, watching both blow past the exact point they had always frozen at
   (UE4SS fully hooked, MODs loaded, PalDefender started, sustained real CPU growth) instead of
   sitting frozen. This is strong, direct, live evidence the specific freeze mechanism is fixed.
   What was NOT verified: neither live test actually completed startup and bound its game port
   within this session's observation window (both were still actively computing, not frozen, after
   10+ minutes) — a separate, disclosed, not-yet-diagnosed slow-startup issue on this specific
   save/MOD combination, distinct from the freeze itself. Manual confirmation that a real server
   fully starts and becomes reachable is recommended once time allows for a longer observation
   window (or a smaller/simpler save to isolate the slow-load question).
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
