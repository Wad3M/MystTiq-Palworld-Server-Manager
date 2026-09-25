# v0.7.95.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and `scripts/Test-v0.7.95.0-Logic.ps1 -RunBuild`, including the frozen v0.7.94.0 checkpoint regression gate, the carried-forward smoke suites, and `MystTiq.LogicHarness` (now including 6 Discord scenarios).
3. New surface: `DiscordBotFormatting`, the bot's live loop/autocomplete/new commands, config fields and validation, Discord Bot card fields.
4. Live check: `GET/PUT /notifications/discord-bot` defaults, 400 on a bad channel id, round-trip, cleanup (scratch server; Default Server read-only).
5. Not covered without a Discord bot token: the gateway connection, status message post/edit, autocomplete replies, presence, events feed.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
