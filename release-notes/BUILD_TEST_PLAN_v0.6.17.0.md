# v0.6.17.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.17.0 logic suite (`scripts/Test-v0.6.17.0-Logic.ps1 -RunBuild`), including the frozen v0.6.16.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Discord Bot, against an isolated `mysttiq-server.exe` instance (never the production directory or process):
   - Confirm `GET /notifications/discord-bot` returns a disabled/unconfigured default on first run.
   - Save a config with a bot token, guild ID, and a role mapping; confirm `GET` returns `tokenConfigured: true` and never echoes the token itself.
   - Save again with a blank token field; confirm the previously stored token is preserved on disk, not wiped.
   - Enable the bot with a bot token: real or, if unavailable, a deliberately invalid one to confirm the connection path genuinely reaches Discord's servers. Confirm `connectionState` reaches `Connected` (real token) or `Failed` within a few seconds (invalid token) rather than retrying forever.
   - If a real bot token and guild are available: register slash commands, run each of the eight commands as users mapped to Viewer/Operator/Admin and as an unmapped user, and confirm both the authorization gate and the underlying action (lifecycle/RCON/moderation) behave correctly.
5. Outbound Discord dispatch: configure the "Discord" notification channel with a webhook URL and confirm a dispatched notification posts a well-formed embed payload.
6. On the Desktop app: confirm the Discord Bot card on the Alert Center page loads/saves configuration and the role-mapping list editor works.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
