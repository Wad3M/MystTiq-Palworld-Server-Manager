# v0.7.47.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.47.0 logic suite (`scripts/Test-v0.7.47.0-Logic.ps1 -RunBuild`), including the frozen v0.7.46.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `HeadlessUe4ssReleaseCatalogService` (new file, `MystTiq.HeadlessHost`), `GET /api/v1/ue4ss/releases`, Desktop DTOs/client method, and the UE4SS page's real release list. This release makes a real outbound network call to GitHub when "Refresh Runtime" is used on the UE4SS page — not required for the build/static gate itself, and it degrades gracefully to an empty list on failure rather than throwing.
5. This version's own research (documented in the architecture doc) included live testing against an isolated copy of the user's real Palworld server, not the live one — no live-server interaction was needed for the actual code that shipped in this version.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
