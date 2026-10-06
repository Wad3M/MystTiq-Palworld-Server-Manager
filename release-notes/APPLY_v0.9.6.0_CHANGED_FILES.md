<!-- MystTiq v1.0.5.0: file reviewed for this release (2026-10-06). -->
# v0.9.6.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.6.0.
- `src/MystTiq.Core/Services/FirewallRules.cs` (new): rule names and server tags, the allow script (tag, old-port
  cleanup), port/protocol/program matching, the rule state, the Linux commands.
- `src/MystTiq.Core/Services/WindowsNetworkDiagnosticsPlatformService.cs`: rules read through `HNetCfg.FwPolicy2`;
  changes through Windows PowerShell by its full path with `-EncodedCommand`; a refusal returns the script to run as an
  administrator. `netstat` by its full path too.
- `src/MystTiq.Core/Services/INetworkDiagnosticsPlatformService.cs`, `LinuxNetworkDiagnosticsPlatformService.cs`,
  `NetworkDiagnosticsService.cs`, `src/MystTiq.Core/Models/NetworkDiagnosticModels.cs`: the server id on repair,
  MystTiq's own rules, `FirewallStatus`.
- `src/MystTiq.Core/Services/WindowsServerSessionInspector.cs`: a process is listed until it has exited (a terminating
  process is no longer dropped when a detail can't be read) and never after.
  `WindowsServerLifecycleService.cs`, `LinuxServerLifecycleService.cs`: status reads and a stop's state writes share a
  lock, and a read during a stop keeps the stop request. `ServerLifecycleStateStore.cs`: one writer at a time, reads
  that don't block a replace, and a refused replace retried. Together: the supervisor no longer restarts a stopped
  server as crashed.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `GET /diagnostics/network/firewall`; repair uses the bound
  port and the server id. `HeadlessDiagnosticsService.cs`: `configuration-game-port` and `network-firewall`
  findings, the `allow-firewall` fix.
- `src/MystTiq.Desktop/Services/ElevatedFirewall.cs` (new), `MystTiqApiClient.cs`, `IMystTiqApiClient.cs`,
  `Models/NetworkDiagnosticDtos.cs`: the status call and the elevated fallback.
- `src/MystTiq.Desktop/Services/MystTiqServiceDiscoveryService.cs`: TCP sweep, ranges, virtual adapters, typed ranges,
  progress. `Services/LaunchArgumentsText.cs` (new): a second server's `-port=`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`: firewall cards on Diagnostics,
  Settings and the wizard's last step, the clone hint, the search's ranges, progress, Cancel and options.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 44 new texts in all 12 languages.
- `scripts/Testing/MystTiq.LogicHarness` (new scenarios; links the search and launch-argument files),
  `scripts/Test-v0.9.6.0-FirewallRoute.ps1` (new smoke), `scripts/Test-v0.9.6.0-Logic.ps1` (new gate); every text
  file re-stamped for v0.9.6.0.
- Docs: `docs/architecture/v0.9.6.0-firewall-and-search.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` and its index, `docs/release/README.md`,
  `docs/release/GITHUB_PRESENTATION.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`, the issue
  templates.