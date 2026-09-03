# v0.4.10.0 Build and Test Plan

1. Clean repository artifacts and terminate only artifact-hosted MystTiq development processes.
2. Run strict release validation with zero errors and zero warnings.
3. Run the v0.4.10.0 logic harness, including player capability truth, route composition, metadata audit privacy, stable-selection, CSV, button-style, relaunch guard, polling, security, and containment checks.
4. Build/publish the existing Windows client, shared Core, Windows/Linux headless hosts, and Windows/Linux Avalonia desktop packages.
5. Run Windows loopback runtime smoke, including metadata persistence and audit evidence.
6. Regenerate the source manifest and verify Full Source and Changed Files archives contain no `artifacts`, `bin`, or `obj` entries.
