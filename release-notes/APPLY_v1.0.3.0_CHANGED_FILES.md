<!-- MystTiq v1.0.5.0: file reviewed for this release (2026-10-06). -->
# v1.0.3.0 Changed Files

- `src/MystTiq.HeadlessHost/BrowserView.cs` (new): the read-only page (HTML, script, style), its content policy, and the
  browser-session registry.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `/web`, `/web/app.js`, `/web/app.css` (no token needed, no
  data in them), `POST /api/v1/auth/browser-login`, the middleware refusing every change from a browser session, and
  browser sessions ended at sign-out.
- `src/MystTiq.HeadlessHost/HeadlessServerDistributionService.cs`: SteamCMD's "Missing configuration" retried once.
- `src/MystTiq.Desktop`: `ViewModels/MainWindowViewModel.BrowserView.cs` (new), the Security page's browser view card,
  2 new texts in all 12 languages.
- `deploy/docker/Dockerfile`, `deploy/docker/entrypoint.sh` (new): the container image.
- `scripts/Test-v1.0.3.0-Docker.ps1` (new): builds and runs the image, and with `-RunServer` the server inside it.
- `scripts/Test-v1.0.3.0-RouteSmoke.ps1` (new): the browser view smoke.
- `scripts/Discover-v1.0.3.0-XboxPlayer.ps1` (new): the read-only Xbox discovery capture.
- Tests: `scripts/Test-v1.0.3.0-Logic.ps1` (generated from the v1.0.2.0 gate, every earlier check carried), a
  LogicHarness scenario and an ArtworkHarness check.
