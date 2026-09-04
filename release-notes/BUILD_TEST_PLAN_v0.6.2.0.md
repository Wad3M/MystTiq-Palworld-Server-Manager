# v0.6.2.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.2.0 logic suite (`scripts/Test-v0.6.2.0-Logic.ps1 -RunBuild`), including the frozen v0.6.1.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated two-server instance (throwaway ports/directories, never the live save/config): configure two server profiles in `mysttiq.json` (`Servers: [...]`, distinct `ServerRoot`/`BackupRoot`/`RuntimeRoot` per entry). Confirm `GET /healthz` reports both `serverProfileIds`. Confirm `GET /api/v1/servers` lists both with independent status. Confirm the classic unprefixed routes (e.g. `GET /api/v1/config`, `GET /api/v1/backups`) resolve to the `"default"` profile, and `GET /api/v1/servers/{id}/...` resolves the named profile, for both profiles. Create a backup on each profile and confirm each lands only in its own `backupRoot`. Dispatch `Server Start` on both profiles simultaneously (true parallel HTTP dispatch) and confirm each fails independently for its own reason, with neither blocked by the other's lock. Call `POST /api/v1/fleet/backup-all` and confirm both profiles produce a backup, staggered by roughly `fleetStaggerSeconds`.
5. Take a real legacy schema-v2 `mysttiq.json` and run `mysttiq-server config-migrate`; confirm it becomes schema v3 with one `"default"` entry in `Servers`, and that the resulting file contains no stray fields (e.g. no serialized `DefaultServer` computed property).
6. On the Desktop app: open the new Fleet page against the two-server test instance; confirm both profiles list with correct names/status, and Backup All / Doctor All / Update All each report per-profile results.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
