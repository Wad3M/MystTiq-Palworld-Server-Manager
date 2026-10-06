<!-- MystTiq v1.0.3.0: file reviewed for this release (2026-10-05). -->
# v0.9.8.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.8.0.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`, `ViewModels/MainWindowViewModel.cs`: the tab strip's
  "»" and "+" in their own columns after the tabs (capped at `TabListMaxWidth`); the brand narrows below 1200 px
  (`ShowBrandSubtitle`, `BrandMinWidth`); the Alert Center's "out of date" rule.
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs`, `LocalManagementApiHost.cs`: the `ComponentOutdated` rule
  and its alerts (`ComponentAlerts`).
- `src/MystTiq.Core/Services/FirewallRules.cs`, `WindowsNetworkDiagnosticsPlatformService.cs`,
  `INetworkDiagnosticsPlatformService.cs`, `NetworkDiagnosticsService.cs`, `Models/NetworkDiagnosticModels.cs`: rules
  against the network profile in use.
- `src/MystTiq.Desktop/Services/MystTiqServiceDiscoveryService.cs`: each adapter's subnet up to a /22.
- `src/MystTiq.Desktop/Models/AlertCenterDtos.cs`, `Assets/i18n/*.json`: the new rule and 9 new texts in all 12
  languages.
- `scripts/Install-MystTiqDesktopLinux.ps1` (new), `Build.ps1` (`DeployDesktopLinux`); `scripts/Deploy-Test-MystTiqDesktopLinux.ps1`
  removed (pinned to v0.3.1.9 and an old address).
- `scripts/Test-v0.9.8.0-UpgradeAccounts.ps1` (new smoke), `scripts/Testing/MystTiq.LogicHarness/Program.cs`,
  `scripts/Testing/MystTiq.ArtworkHarness/Program.cs` (new scenarios), `scripts/Test-v0.9.8.0-Logic.ps1` (new gate); every
  text file re-stamped for v0.9.8.0.
- Docs: `docs/architecture/v0.9.8.0-small-screens-alerts-linux.md`, the release-notes trio, `CHANGELOG.md`,
  `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` and its index, `docs/release/README.md`,
  `docs/release/GITHUB_PRESENTATION.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`, the issue
  templates.