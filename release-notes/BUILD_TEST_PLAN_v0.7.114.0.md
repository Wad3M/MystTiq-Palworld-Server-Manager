# v0.7.114.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run strict validation and `scripts/Test-v0.7.114.0-Logic.ps1 -RunBuild`, including the frozen v0.7.113.0
   checkpoint gate, every carried-forward smoke (now including v0.7.113.0's), the new v0.7.114.0 smoke, and
   `MystTiq.LogicHarness`.
3. New surface: `HeadlessUserAccountService`, the session-token branch of the auth middleware, the anonymous
   `/api/v1/auth/login` path, `/api/v1/auth/*` and `/api/v1/security/users*`, the Desktop Sign In block and User
   Accounts card, `whoami` on connect.
4. After the smoke, confirm `C:\ProgramData\MystTiqPalworldServer\fleet\security\users.json` was NOT created
   (the smoke must use its own FleetRoot).
5. Live: both new UI blocks render. Against a remote MystTiq with authentication on: create an account, sign
   in from the Desktop, confirm the role shows and Admin-only cards grey out for an Operator (not possible on
   this machine today; recorded as not verified).
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
