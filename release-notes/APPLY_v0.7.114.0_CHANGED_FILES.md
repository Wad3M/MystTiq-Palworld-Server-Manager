# v0.7.114.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.114.0
- `src/MystTiq.HeadlessHost/HeadlessUserAccountService.cs` (new) — accounts, PBKDF2 passwords, hashed sessions,
  lockout, records
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — service wiring, middleware (session tokens, anonymous
  sign-in path, actor on the API audit line), `/api/v1/auth/*` and `/api/v1/security/users*` routes
- `src/MystTiq.Desktop/Models/RbacDtos.cs` — `UserAccountDto`, `UserAccountResultDto`, `UserLoginResultDto`
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs` — sign-in/out, own password,
  account management; `/api/v1/auth/` added to the fleet-only routes
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — sign-in state and commands, User Accounts state and
  commands, `whoami` on connect, `ApplyCurrentPrincipal`
- `src/MystTiq.Desktop/MainWindow.axaml` — Sign in block on Settings, User Accounts card on Security
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 3 new scenarios
- `scripts/Test-v0.7.114.0-RouteSmoke.ps1` (new), `scripts/Test-v0.7.114.0-Logic.ps1` (new)
- `docs/architecture/v0.7.114.0-multi-user-login.md`, `release-notes/v0.7.114.0.md`,
  `release-notes/APPLY_v0.7.114.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.114.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
