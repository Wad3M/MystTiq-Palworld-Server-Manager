# v0.6.0.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.0.0 logic suite (`scripts/Test-v0.6.0.0-Logic.ps1 -RunBuild`), including the frozen v0.5.1.5 checkpoint regression gate.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Run the isolated Windows API runtime smoke suite.
5. On an isolated copy of a real guild/base-populated save (never the live save), dispatch two conflicting Apply calls simultaneously (true parallel dispatch, not just back-to-back — a plain sequential fire can miss the lock window entirely since each service releases it in its own `finally` before the HTTP response returns) — e.g. a Base Recovery Apply against a World Transaction Apply — and confirm the loser is rejected with the coordinator's "Blocked: resource 'world-mutation' is held by operation ... until it completes" message, not some other pre-existing per-service guard's message.
6. Confirm the Desktop World Transactions page's new "Operation Platform" card shows entries (Kind/Source/Phase/TransactionState/UpdatedUtc) from all three migrated features after exercising each once, and that its Refresh command updates the list.
7. Deploy to the Linux VM (`192.168.1.144`) and confirm the new `/api/v1/operations` (list) and `/api/v1/operations/{id}` (404) routes work against the live systemd-managed service — this release touches real backend/API code, so the standing cross-platform rule applies for real.
8. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
