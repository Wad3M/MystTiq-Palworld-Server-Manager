<!-- MystTiq v1.0.0.5: file reviewed for this release (2026-10-05). -->
# v1.0.0.1 Changed Files

## Launcher workspace

- `src/MystTiq.Desktop/Models/NavigationPage.cs` — adds the Launcher page without changing existing saved numeric page identities.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — includes Launcher in the Server navigation/category and exposes the Launcher page state.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.Launcher.cs` — launcher editor, effective-command preview, process options, custom arguments, and Default / No Mods / Show Window presets.
- `src/MystTiq.Desktop/MainWindow.axaml` — moves Advanced Startup into **Server > Launcher** and adds the preset controls.
- `src/MystTiq.Desktop/Assets/i18n/*.json` — Launcher navigation/page strings in all shipped language catalogs.
- `src/MystTiq.Desktop/Services/ArtworkCatalog.cs` — Launcher page artwork/category integration.

## Server launch and console behavior

- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs` — parses MystTiq-only launcher metadata, supports selectable executable/working-directory/process options, removes `@mysttiq:*` entries before PalServer launch, and retains manual-parity defaults for older profiles.
- `src/MystTiq.Core/Services/LinuxServerLifecycleService.cs` — strips Windows launcher metadata before constructing Linux server arguments.
- `src/MystTiq.Core/Models/HeadlessConfiguration.cs` and `src/MystTiq.Core/Services/HeadlessConfigurationService.cs` — preserve launcher arguments without forcing the older fixed startup flags.
- `src/MystTiq.HeadlessHost/HeadlessMonitoringService.cs` — includes `Identity-Diagnostic.log` as the Palworld console source.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — carries the saved launcher configuration through the management API.

## Release/version files

- `Directory.Build.props` and `src/MystTiq.Desktop/app.manifest` — version 1.0.0.1.
- `README.md`, `docs/index.html`, `CHANGELOG.md`, `release-notes/v1.0.0.1.md` — v1.0.0.1 release documentation.
- `release-notes/BUILD_TEST_PLAN_v1.0.0.1.md` and this file — release-validator-required v1.0.0.1 documents.
- `scripts/Test-v1.0.0.1-Logic.ps1` — current-version release gate for the Launcher workspace change.

## v1.0.0.1 polish addendum (2026-10-02)

- `src/MystTiq.Desktop/MainWindow.axaml` / `.axaml.cs` — single-click title language picker, Console startup-window capture controls, Steam identity display/profile link, Launcher icon binding.
- `src/MystTiq.Desktop/Models/ConsoleCaptureDtos.cs` — Desktop DTOs for native startup-window capture status/actions.
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs` and `MystTiqApiClient.cs` — console-capture API client operations.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ConsoleCapture.cs` — Console capture UI state and commands.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — refresh integration plus Steam ID/User ID player search and CSV export.
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs` and `LocalManagementApiHost.cs` — expose live/persisted Steam ID and Palworld User ID in Player Explorer.
- `src/MystTiq.Desktop/Models/PlayerGuildExplorerDtos.cs` — Steam identity fields and Steam profile URL.
- `src/MystTiq.Desktop/Assets/Icons/icon-launcher.png` — dedicated Launcher rocket artwork.
- `src/MystTiq.Desktop/Assets/i18n/*.json` — Launcher/console-capture/Steam identity UI keys.
- `scripts/Build-AvaloniaDesktop.ps1` — best-effort build/staging of the optional native console-capture proxy on Windows.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs` — expects and validates the new Launcher navigation icon.

## Identity guard, tray, stuck starts and addresses (2026-10-04)

- `src/MystTiq.Core/Services/SteamPlayerUid.cs`: the player ID a Steam ID gives (CityHash64, inputs up to 64 bytes).
- `src/MystTiq.HeadlessHost/HeadlessIdentityGuardService.cs`: catches a Steam player not given their existing character;
  records, notifies and kicks (default on); `GET`/`PUT /players/identity-guard`.
- `src/MystTiq.Desktop/MainWindow.axaml.cs`, `App.axaml(.cs)`, `ViewModels/MainWindowViewModel.Exit.cs`,
  `Services/LocalManagementBootstrapper.cs` (`OwnedHelperEndpoint`), `Models/FleetDtos.cs` (`Phase`): close goes to the
  tray; Exit stops every server the owned helper runs, then the helper. `Views/ConfirmMinimizeToTrayDialog.axaml(.cs)` and
  the "Exit GUI only" tray item are removed.
- `src/MystTiq.Core/Services/StartupWatch.cs`, `Models/ServerLifecycleModels.cs` (`NativeStartedAt`, `StartupStuck`),
  `Windows/LinuxServerLifecycleService.cs`: how long a start has run, and stuck after two minutes.
- `src/MystTiq.HeadlessHost/HeadlessModSafeStartService.cs`: test-load mode, testing from a stuck start, checked switches,
  timings; `POST /mods/safe-start?mode=testload`.
- `src/MystTiq.Core/Services/HostAddresses.cs`, `WanReachabilityService.cs` (`GetRouterWanAddressAsync`),
  `src/MystTiq.HeadlessHost/HeadlessAddressService.cs`: `GET /network/addresses`.
- Desktop: `MainWindowViewModel.Addresses.cs`, `Services/StuckStartText.cs`, `MainWindow.axaml` (the stuck-start panel, the
  addresses line, accessible names on the Launcher page), `Models/ServerStatusDto.cs`, `ModManagementDtos.cs`,
  `HostDtos.cs`, API client calls.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 40 new texts in all 12 languages.
- Tests: `scripts/Test-v1.0.0.1-Logic.ps1` (generated from the v1.0.0.0 gate, every earlier check carried),
  `scripts/Test-v1.0.0.1-RouteSmoke.ps1`, `scripts/Testing/FakePalServer` (hangs while "HangsStartup" is on), logic and
  artwork harness scenarios.
