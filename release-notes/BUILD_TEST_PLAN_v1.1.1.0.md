<!-- MystTiq v1.1.1.2: file reviewed for this release (2026-10-19). -->
# v1.1.1.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.1.0-Logic.ps1 -RunBuild -RunLong`: the frozen v1.1.0.3 gate, every earlier contract, the v1.1.1.0
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. LogicHarness: the player checks without a desktop (the service runs them when no desktop polls, holds back while one
   does, never runs twice at once, logs a failure once, starts and stops); every account (an Xbox account's character from
   the player list; links by hand); the new-character notice (once, then let through, again after 30 minutes).
4. ArtworkHarness: MOD Sources holds the Steam Workshop card, the MOD browser and the Nexus Mods catalog, MOD Library the
   ZIP install and load order; a Nexus link opens MOD Sources; no Like double-click preset; a tab switch to a server with
   the same theme leaves the theme resources alone (300 switches in well under 3 minutes) and another theme still applies.
5. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
6. `scripts/New-MystTiqCheckpoint.ps1`, then extract the FullSource ZIP into an empty folder and run
   `Test-v1.1.1.0-Logic.ps1` there (static).
