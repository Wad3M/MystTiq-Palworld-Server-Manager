# v0.6.13.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.13.0 logic suite (`scripts/Test-v0.6.13.0-Logic.ps1 -RunBuild`), including the frozen v0.6.12.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Fleet-wide crash recovery, against a real isolated copy of production-derived data with two profiles (source + a Clone World target):
   - Start both profiles' real PalServer processes; confirm both bind distinct game ports.
   - Force-kill one profile's process tree (both the Cmd wrapper and its child) directly; confirm the crash-recovery loop detects the loss and restarts it with a fresh PID within the configured backoff window.
   - Confirm the untouched sibling profile is never affected.
   - Explicitly stop a profile via its real Stop route; poll repeatedly across multiple recovery-loop cycles and confirm it is never auto-restarted.
5. `--server-id` and per-profile service naming:
   - Confirm `status`/`start`/`stop`/`restart`/`service-*` all resolve the target profile from `--server-id`, falling back to the default profile when omitted.
   - Confirm a non-default profile's `WindowsServiceManager`/`LinuxSystemdServiceManager` produce a distinct, suffixed service/unit name, and the default profile's name is byte-identical to prior versions.
   - If Administrator elevation is available: `service-install --server-id <profile>` and confirm a second, independently-named service is created without touching an existing default-profile service; `service-uninstall --server-id <profile>` removes only that one.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
