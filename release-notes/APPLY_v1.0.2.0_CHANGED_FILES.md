<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# v1.0.2.0 Changed Files

- `src/MystTiq.Core/Services/FrozenServerWatchdog.cs` (new): when a running server counts as frozen; the probe interface.
- `src/MystTiq.Core/Services/HeadlessSupervisor.cs`: frozen-server checks and restarts in the recovery loop; the restart
  outcome reporting shared with crash recovery.
- `src/MystTiq.Core/Services/SupervisorObserver.cs`: `FrozenDetected`.
- `src/MystTiq.Core/Models/HeadlessSupervisorOptions.cs`, `HeadlessConfiguration.cs`, `Services/HeadlessConfigurationService.cs`:
  `UnresponsiveLimit` / `lifecycle.unresponsiveRestartSeconds` (absent 180, 0 off, otherwise at least 60).
- `src/MystTiq.HeadlessHost/PalworldRestResponsivenessProbe.cs` (new): GET `/v1/api/info` against the server's REST API.
- `src/MystTiq.HeadlessHost/CrashAlerts.cs`: the frozen-server alert and its Activity entry.
- `src/MystTiq.HeadlessHost/HeadlessFleetCrashRecoveryService.cs`, `LocalManagementApiHost.cs`, `Program.cs`: the probe
  wired into every supervisor (api-run and service-run).
- `src/MystTiq.HeadlessHost/NotificationDeliveryLog.cs` (new): every outside send and each channel's health.
- `src/MystTiq.HeadlessHost/HeadlessNotificationRoutingService.cs`: sends return and record their result.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `GET notifications/delivery-health`.
- `src/MystTiq.HeadlessHost/HeadlessMonitoringService.cs`: the settings readers shared with the probe.
- `src/MystTiq.Desktop`: `Models/AlertCenterDtos.cs`, `ViewModels/MainWindowViewModel.Delivery.cs` (new), the Dashboard
  warning and the Alert Center Delivery card in `MainWindow.axaml`, `Services/StatusTags.cs` (Delivered, Failing, Not
  proven), the API client.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 15 new texts in all 12 languages.
- `scripts/Test-v1.0.2.0-LinuxSystemd.ps1` and `.sh` (new): the systemd test on the Linux VM.
- Tests: `scripts/Test-v1.0.2.0-LinuxSystemd.ps1` runs from the gate when the VM answers. `scripts/Test-v1.0.2.0-Logic.ps1`
  is generated from the v1.0.1.0 gate, with every earlier check carried. Six LogicHarness scenarios and four
  ArtworkHarness checks are added.
