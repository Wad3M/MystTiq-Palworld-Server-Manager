# v0.6.12.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.12.0 logic suite (`scripts/Test-v0.6.12.0-Logic.ps1 -RunBuild`), including the frozen v0.6.11.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Sidecar-staleness fix, against a real isolated copy of production-derived save data (never the production directory or process):
   - Note `Level.sav.json`'s modified timestamp before any mutation.
   - Apply a real guild ownership operation (e.g. Transfer Leadership) via `POST /guilds/ownership/preview` then `POST /guilds/ownership/apply`.
   - Confirm the sidecar's modified timestamp changed and a fresh `GET /world/players-guilds` immediately reflects the mutation with no other action taken.
   - Repeat for at least one Base Ownership operation and the Character Migration commit path.
5. `config-write-default` override fix:
   - Run `config-write-default --server-root <path> --steamcmd <path> --backup-root <path> --runtime-root <path> --overwrite` against a scratch config path.
   - Confirm the written JSON's `Servers[0]` reflects every override passed, not the hardcoded built-in default.
6. Bug-testing pass against a fresh isolated instance: malformed JSON body, empty body, invalid `operationType`, an expired/garbage preview token, a nonexistent server-profile route, and cloning a profile onto itself — confirm every case returns a clean 4xx with no unhandled exception or stack trace.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
