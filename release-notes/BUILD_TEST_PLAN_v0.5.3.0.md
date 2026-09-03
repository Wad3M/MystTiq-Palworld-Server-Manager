# v0.5.3.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.5.3.0 logic suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. Exercise Preview/Apply Base Ownership Transfer against a disposable copy of a real guild/base-populated save (never the live save); independently re-decode the result rather than trusting the service's own report.
6. Repeat step 5 against an isolated standalone instance on the reference Linux host (192.168.1.144), reusing its already-installed `palsav`/`palooz` converter toolchain; independently re-decode and confirm the result matches Windows.
7. Relaunch the Windows desktop and visually verify the Base Ownership Transfer card (target-guild input, preview, confirm, apply) and the honest Base Recovery stub on the Bases page.
8. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion. The known systemd `User=root` gap on the Linux VM (causing the automated `Test-v0.5.3.0-LinuxAcceptance.sh` suite to fail several unrelated checks) is pre-existing and does not block this release; see release-notes/v0.5.3.0.md.
