# v0.6.1.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.1.0 logic suite (`scripts/Test-v0.6.1.0-Logic.ps1 -RunBuild`), including the frozen v0.6.0.0 checkpoint regression gate.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. On a real isolated authenticated Windows instance (never the live save/config): confirm the legacy bearer token still authenticates and resolves to `Owner`, and that a missing/wrong token is rejected with 401. Create an automation rule with a `SendNotification` action; confirm `run-now` produces a `Completed` run and a real notification, and confirm a short-interval rule also fires on its own from the background scheduler (wait for it, don't just trigger it). Create an `Operator`-role principal; confirm it can create a backup but is rejected with 403 on an `Admin`-gated route (e.g. automation rule creation). Dispatch a Server Start and a World Transaction Apply simultaneously (true parallel HTTP dispatch, not sequential) and confirm exactly one is rejected by the coordinator citing `world-mutation` — this proves the lifecycle-route retrofit, not just that the coordinator class itself works.
6. Deploy to the Linux VM (`192.168.1.144`). Because the ephemeral `mystroth`-run acceptance-script instance hits the pre-existing root-owned-runtime-directory gap at startup now (the new services create their state directories in their constructors), also run a targeted manual check: start `mysttiq-server` as `mystroth` against a fully isolated, `mystroth`-owned runtime root, and exercise `/healthz`, `/api/v1/security/whoami`, automation rule create + run-now + runs, `/api/v1/notifications`, `/api/v1/alerts/predictions`, and `/api/v1/backups` directly.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
