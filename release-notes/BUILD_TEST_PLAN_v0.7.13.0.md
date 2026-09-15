# v0.7.13.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.13.0 logic suite (`scripts/Test-v0.7.13.0-Logic.ps1 -RunBuild`), including the frozen v0.7.12.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the v0.7.12.0 route smoke script, and the whitelist enforcement harness — all carried forward unchanged, since this release is Desktop-only.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. `MainWindow.axaml` must build clean under Avalonia's XAML compiler — this release rewrote large sections of it; watch specifically for the recurring `--`-inside-an-XML-comment AVLN1001 parse error (two instances were introduced and fixed while authoring this release's new comments).
5. Live multi-server verification performed standalone before being folded into this plan: two isolated `--desktop-sidecar` loopback instances on different ports running simultaneously, plus a third instance configured with a generated bearer token, a self-signed TLS certificate, and a direct bind to the machine's real LAN address (not `--desktop-sidecar`, which force-binds loopback by design) — confirming the secured instance's `/healthz` reports `authentication:true`/`tls:true`, an unauthenticated request is rejected with 401, and it is reachable exactly as a genuinely separate remote machine's client would reach it.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
