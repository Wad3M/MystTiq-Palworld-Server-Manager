<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.6.0 Changed Files

- `src/MystTiq.Desktop/Services/NexusOAuth.cs` (new): OAuth 2.0 with PKCE against `users.nexusmods.com` (authorize, the
  loopback callback on `127.0.0.1:47823/nexus/callback`, the token exchange and refresh), the access token's RS256
  signature checked with Nexus's public key, the tokens kept in `CredentialStore`, the old personal key deleted.
- `src/MystTiq.Desktop/Services/NexusModsClient.cs`: `Authorization: Bearer` with the access token instead of the
  `apikey` header; a handler for tests.
- `src/MystTiq.Desktop/Services/ModSources.cs`: the MOD browser's Nexus source asks the sign-in for a token.
- `src/MystTiq.Desktop/ViewModels/ModsViewModel.cs`, `MainWindow.axaml`: Sign in with Nexus Mods and Sign out instead of
  the key box, Connect, Save Key and Forget Key.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 31 `nexus.*` strings in all 12 languages.
- `Directory.Build.props` (`NexusOAuthClientId`, the Client ID Nexus issues; version 1.1.6.0), `MystTiq.Desktop.csproj`
  (passes it to the app), `src/MystTiq.Desktop/app.manifest`.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: the N-1 check against a scripted Nexus; `scripts/Test-v1.1.6.0-Logic.ps1`.
- Documentation: these notes, `release-notes/BUILD_TEST_PLAN_v1.1.6.0.md`, `docs/architecture/v1.1.6.0-nexus-oauth.md`,
  `PRIVACY.md`, `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/release/README.md`, `docs/roadmap/PRODUCT_ROADMAP.md`,
  `CHECKPOINT_NOTES.md`; the two example version strings; every text file's review stamp.
