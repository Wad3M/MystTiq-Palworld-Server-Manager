# v0.6.19.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.19.0 logic suite (`scripts/Test-v0.6.19.0-Logic.ps1 -RunBuild`), including the frozen v0.6.18.4 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): `TabSession` exists with the expected per-tab fields; `SelectedProfile`/`BearerToken`/`ManagementApiConnected`/`ConnectionState`/`IsBusy`/`ServerIsRunning` are backed by `ActiveTab`, not a private field; the old single shared refresh timer is gone in favor of one timer per tab; the "+" flow offers both a new-server and connect-existing path; the existing-profile connect list is filtered against currently open tabs.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion. Real interactive confirmation (opening two tabs, confirming both stay connected independently, confirming a third tab can't connect to an already-open profile, confirming status dots update independently) is the user's to do once built — the same standing GUI verification gap disclosed since v0.6.4.0, more consequential here than for any prior milestone this session.
