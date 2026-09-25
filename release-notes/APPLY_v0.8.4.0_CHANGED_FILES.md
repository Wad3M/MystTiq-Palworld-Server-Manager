# v0.8.4.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.4.0.
- `src/MystTiq.HeadlessHost/HeadlessNotificationRoutingService.cs`: the delivery pause.
  - `NotificationDeliveryState`, `NotificationDeliveryPolicy`, `GetDeliveryState` and `PauseDelivery`.
  - The pause is stored in `delivery.json`.
  - `Dispatch` skips outside channels while paused, and logs each skip.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: three new routes.
  - `GET /notifications/delivery`.
  - `POST /notifications/delivery/pause` (Admin).
  - `POST /notifications/test` (Admin).
- `src/MystTiq.Desktop/Models/AlertCenterDtos.cs`: the delivery DTOs.
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs` and `MystTiqApiClient.cs`: three client methods.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: `NotificationDelivery`, `PauseDeliveryCommand` and
  `SendTestNotificationCommand`. The state is loaded with the Alert Center.
- `src/MystTiq.Desktop/MainWindow.axaml`: the "Pause Discord, Email & Webhooks" card.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: 2 "Delivery pause" scenarios.
- New scripts: `scripts/Test-v0.8.4.0-RouteSmoke.ps1`, `scripts/Test-v0.8.4.0-Logic.ps1`.
- New docs:
  - `docs/architecture/v0.8.4.0-delivery-pause.md`
  - `release-notes/v0.8.4.0.md`
  - `release-notes/APPLY_v0.8.4.0_CHANGED_FILES.md`
  - `release-notes/BUILD_TEST_PLAN_v0.8.4.0.md`
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
