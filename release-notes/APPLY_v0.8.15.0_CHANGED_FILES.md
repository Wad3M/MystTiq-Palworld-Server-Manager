# v0.8.15.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.15.0.
- `src/MystTiq.Desktop/Services/AccessRefusal.cs` (new): recognises a 401/403 refusal (`error`, empty or non-JSON body)
  and describes it.
- `src/MystTiq.Desktop/Services/MystTiqApiClient.cs`: `ReadOperationAsync` throws a refusal with its status code instead
  of parsing it as the route's result. The sign-in route's own 401 result is unchanged.
- New: the remote sign-in test.
  - `scripts/Test-v0.8.15.0-RemoteSignIn.ps1` (Windows orchestrator);
  - `scripts/Test-v0.8.15.0-RemoteSignIn.setup.sh` and `.teardown.sh` (the isolated instance on the VM);
  - `scripts/Testing/MystTiq.RemoteSignInHarness/` (the Desktop's own ViewModel, window and client, headless).
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: `AccessRefusal` checks.
- New: `scripts/Test-v0.8.15.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.15.0-remote-sign-in.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
