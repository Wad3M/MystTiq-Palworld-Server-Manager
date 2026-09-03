# MystTiq Palworld Server Manager
## GUI Reconstruction & Functional Wiring Guide
### Historical reference: v0.2.16.4 → Current build: v0.4.6.3

## Purpose

Use **v0.2.16.4 as the visual/workflow reference** and **v0.4.6.3 as the architecture/backend authority**.

The current application has an Avalonia desktop client (`MystTiq.Desktop`) communicating with a headless management host (`MystTiq.HeadlessHost`). Reproducing the old GUI must **not** mean restoring old WPF code-behind as the primary implementation.

> **Old GUI tells us what the user should see and what workflows existed. Current services/API tell us how supported operations must now be performed.**

## Verified structural findings

- Historical WPF GUI event bindings discovered: **287**
- Unique historical handlers: **253**
- Current Avalonia command bindings discovered: **67**
- Unique current Avalonia command names: **37**
- Current `IMystTiqApiClient` methods discovered: **32**
- Current headless API routes discovered: **34**
- The current sidebar already mirrors most historical high-level navigation.
- Current navigation uses stable `NavigationPage` enum values rather than old tab indexes. Preserve this.
- `RefreshPageForNavigationAsync()` already restores page-entry refresh for most migrated pages.
- Crash Analyzer and Palworld Save Tools are explicit migration placeholders.
- Current world/player/guild exploration is read-only in several areas where the historical GUI had mutation/recovery tools.

## Architectural boundaries that must not regress

1. **Lifecycle:** Avalonia commands → `IMystTiqApiClient` → headless lifecycle API. Never add direct PalServer process launching to the GUI.
2. **Authentication/TLS/remote:** Keep connection profiles, bearer tokens, service discovery, local bootstrap and remote/LAN behavior.
3. **World mutations:** Historical repair/recovery/ownership controls require server-side preview, backup, transaction, validation and audit before being restored.
4. **Remote-safe filesystem behavior:** Historical Open Folder/Browse actions cannot assume GUI and server share a filesystem.
5. **Unsupported administration:** Do not fake operations the current headless service explicitly reports as unsupported.

## Page-by-page migration crosswalk

| Area | Historical workflow | Current mapping | Status | Required reconstruction behavior |
|---|---|---|---|---|
| Dashboard | START | `StartCommand` → `IMystTiqApiClient.StartServerAsync` | **REWIRE** | Preserve lifecycle state refresh and authoritative status; do not launch PalServer directly from GUI. |
| Dashboard | RESTART | `RestartCommand` → `IMystTiqApiClient.RestartServerAsync` | **REWIRE** | Already migrated. |
| Dashboard | STOP | `StopCommand` → `IMystTiqApiClient.StopServerAsync` | **REWIRE** | Already migrated. |
| Dashboard | BACKUP | `CreateBackupCommand` → `IMystTiqApiClient.CreateBackupAsync` | **REWIRE** | Already migrated. |
| Dashboard | DOCTOR | `NavigateCommand(Doctor)` → `RunDoctorAsync on page entry` | **REWIRE** | Navigation + page-entry execution already exists. |
| Server Setup / Update | Update / Verify / Install | `RefreshDistributionCommand / PreviewDistributionPlanCommand / UpdatePalworldServerCommand` → `Server distribution API` | **ADAPT** | Current model consolidates several old environment actions. Recreate old cards/actions visually but route to distribution service. |
| Configuration | Load Active | `LoadPalworldConfigurationCommand` → `GetPalworldConfigurationAsync` | **REWIRE** | Current service is authoritative. |
| Configuration | Save Changes | `SavePalworldConfigurationCommand` → `SavePalworldConfigurationAsync` | **REWIRE** | Restore old simple/advanced visual organization without reverting persistence code. |
| Configuration | Search/category/QoL presets/import/export | `No equivalent command/binding found` → `No current endpoint` | **REIMPLEMENT/ADAPT** | High-value GUI parity gap. Implement locally around current setting DTOs where safe; add endpoint only if server-side capability is genuinely required. |
| Console | Refresh/log tail | `RefreshMonitoringCommand` → `GetStatusPollingAsync/GetLogTailAsync` | **REWIRE** | Core log viewing exists. |
| Console | Severity/category/search/pause/clear/export/open logs | `No full equivalent` → `Client-side filtering + optional local shell helper` | **REIMPLEMENT** | These can mostly be client-side features; remote 'open folder' must be treated differently. |
| Console | RCON doctor/connect/disconnect/send | `No equivalent found` → `No current API contract` | **REIMPLEMENT** | Do not fake. Add a headless RCON service/API if this workflow is to return. |
| Players | Refresh | `RefreshMonitoringCommand` → `GetPlayersAsync/GetStatusPollingAsync` | **REWIRE** | Already present. |
| Players | Kick/Ban | `KickSelectedPlayerCommand / BanSelectedPlayerCommand` → `RunPlayerAdminActionAsync` | **REWIRE** | Supported by current headless Palworld REST bridge. |
| Players | Whisper/Promote/Give Item | `Commands exist but backend intentionally reports unsupported` → `RunPlayerAdminActionAsync` | **KEEP DISABLED/EXPLAIN** | Current headless service explicitly refuses unsupported vanilla operations; GUI should show reason rather than pretending success. |
| Players | Temp ban/unban/admin removal/whitelist/notes/warnings/save discovery/export | `No current equivalent found` → `No current endpoint for most` | **REIMPLEMENT/ADAPT** | Restore incrementally with explicit provider/capability detection and audit logging. |
| Guilds | Read-only discovery | `RefreshPlayerGuildExplorerCommand` → `GetPlayerGuildExplorerAsync` | **REWIRE** | Current backend has authoritative decoded GroupSaveDataMap exploration. |
| Guilds | Add player/transfer leader/claim orphan/repair mappings/apply repair | `No mutation commands in new client` → `No mutation API` | **REIMPLEMENT** | Must use backup/preview/transaction safety before any save mutation. |
| Backups | Create/refresh/restore/delete | `Create/Refresh/Restore/Delete backup commands` → `Backup API` | **REWIRE** | Core migrated. |
| Backups | Verify selected/all, retention preview/apply, open root | `No direct equivalents found` → `Potential local/headless additions` | **REIMPLEMENT/ADAPT** | Verification/retention should be server-side for remote-safe behavior. |
| MOD Dashboard/Library | Refresh/verify/enable/disable | `RefreshModsCommand/VerifyModsCommand/EnableSelectedModCommand/DisableSelectedModCommand` → `MOD API` | **REWIRE** | Core migrated. |
| MOD Library | Install ZIP/delete/enable all/disable all/repair/local Steam import/web info/workshop | `No equivalents found` → `No current API contracts` | **REIMPLEMENT** | Restore as separate MOD management phase. |
| UE4SS | Basic inventory/health | `RefreshModsCommand / UE4SS page data` → `UE4SS endpoint` | **REWIRE** | Read-only/runtime health can use current endpoint. |
| UE4SS | Version source/releases/install/import/backup/restore/toggle | `No equivalent found` → `No mutation API` | **REIMPLEMENT** | Needs dedicated safe UE4SS management API. |
| World Inspector | Inspect active world | `RefreshWorldExplorerCommand` → `GetWorldExplorerAsync` | **REWIRE** | Read-only exploration migrated. |
| World Inspector | Nested files/health/integrity/validator/repair/transactions/import | `Mostly absent in Avalonia client` → `No matching current APIs found` | **REIMPLEMENT** | Major parity gap; preserve transaction/safety-backup architecture from historical design concepts. |
| Bases | Read-only evidence | `RefreshPlayerGuildExplorerCommand` → `GetPlayerGuildExplorerAsync` | **ADAPT** | New client is explicitly read-only. |
| Bases | Ownership preview/apply/plan/export/backup | `No equivalent found` → `No current mutation API` | **REIMPLEMENT** | Never direct-edit from GUI; require server-side transaction/backup/validation. |
| Recovery | Player/guild/base recovery workflows | `No current navigation page/API` → `No current mutation API` | **REIMPLEMENT** | Restore later as explicit administration workflows; current UI states these remain later. |
| Activity & Audit | Refresh/tail | `RefreshActivityCommand` → `GetActivityLogTailAsync` | **REWIRE** | Core migrated. |
| Activity & Audit | Search/severity/category/export/clear | `No full equivalents` → `Mostly client-side except clear semantics` | **REIMPLEMENT/ADAPT** | Filtering/export can be client-side; clearing persistent audit requires careful policy. |
| Notifications | Bell/flyout/mark read/pin/dismiss/self-test/export | `No notification subsystem in current Avalonia mapping` → `No endpoint` | **REIMPLEMENT** | Missing page/workflow, not merely styling. |
| Crash Analyzer | Historical crash history/stability/isolation controls | `Placeholder page` → `No equivalent API` | **REIMPLEMENT** | Explicitly marked not fully migrated in v0.4.6.3. |
| Palworld Save Tools | Historical tool self-tests/converter/python/world tests | `Placeholder page` → `No equivalent API` | **REIMPLEMENT** | Explicitly marked not fully migrated in v0.4.6.3. |
| Workspace | Path inspection/refresh | `LoadConfigurationCommand` → `GetEditableConfigurationAsync` | **ADAPT** | Current client can inspect configuration-backed paths. |
| Workspace | Browse/open/validate/save paths | `Partial load/save only` → `SaveEditableConfigurationAsync` | **ADAPT** | Local browse/open must respect whether GUI is local or remote; remote path opening cannot use local shell. |
| Diagnostics Center | Network/listener/firewall checks | `RunNetworkDiagnosticsCommand` → `GetNetworkDiagnosticsAsync` | **ADAPT** | Current diagnostics focus is network/firewall/listener and is functional. |
| Diagnostics Center | Firewall repair / restart | `RepairFirewallCommand / RestartFromDiagnosticsCommand` → `RepairNetworkFirewallAsync / RestartFromNetworkDiagnosticsAsync` | **REUSE** | New functionality should be retained even if absent from old GUI. |
| Diagnostics Center | Export/support package/copy/open diagnostics | `Copy report code-behind exists; export/support package partial/absent` → `Mixed` | **ADAPT/REIMPLEMENT** | Keep the new copy-report function; restore richer report/support workflow. |
| Settings | Connection profiles/API endpoint/auth/TLS | `SaveProfileCommand/DiscoverServicesCommand/ConnectCommand` → `Profile store + API client + discovery/bootstrapper` | **KEEP NEW** | Blend old styling with current connection-profile functionality; do not regress remote/LAN/TLS/session behavior. |

## Recommended implementation sequence

### Phase A — Recreate visual parity around already-migrated functionality
Dashboard; Server Setup/Update Center; Configuration; Console/log viewing; Players; read-only Guilds/Bases; Backups; MOD Dashboard/basic MOD Library; UE4SS health; World Inspector read-only; Settings; Activity & Audit; Doctor; Diagnostics; Workspace.

### Phase B — Restore client-only parity features
Search/filter bars, sort/view modes, pause/clear visible console, CSV/report exports, copy actions, selection details and other non-destructive presentation logic.

### Phase C — Add missing headless APIs before enabling controls
RCON; backup verify/retention; advanced MOD management; UE4SS management; guild/base mutations; recovery; validator/repair/import; transaction center; notifications; crash analyzer; Save Tools; support packages.

### Phase D — Re-enable safety-sensitive actions
Use the mandatory sequence: **Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh UI**.

## Per-control workflow for the AI coding helper

1. Locate the historical element in `src/PalworldManager/MainWindow.xaml`.
2. Record its old event handler.
3. Read the old handler only to understand intent and UX sequence.
4. Search `MystTiq.Desktop.MainWindowViewModel` for an existing command.
5. Search `IMystTiqApiClient` for an existing API client method.
6. Search `MystTiq.HeadlessHost.LocalManagementApiHost` for the route.
7. Search the corresponding headless service for the authoritative implementation.
8. If all layers exist, REWIRE the recreated control.
9. If the backend exists but the UI binding is absent, add a ViewModel command; do not duplicate backend logic.
10. If the feature is client-only, implement it in Avalonia.
11. If it changes server/world state and no backend exists, mark BACKEND REQUIRED and implement/test the headless service first.
12. If the backend reports an operation unsupported, show that limitation instead of simulating success.

## Required per-control documentation template

```text
Page:
Section:
Historical visual label:
Historical x:Name:
Historical handler:
Historical intent:

Current NavigationPage:
Current ViewModel command:
Current API client method:
Current API route:
Current headless service:
Classification: REUSE | REWIRE | ADAPT | REIMPLEMENT | OBSOLETE | VISUAL ONLY

Inputs:
CanExecute / enabled conditions:
Confirmation:
Success UI:
Failure UI:
Refresh after action:
Audit/log expectation:
Remote-safe:
Safety backup required:
Automated tests:
```

## Definition of complete for a reconstructed page

- Correct `NavigationPage` selection.
- Historical visual hierarchy is recognizably reproduced.
- Every visible operational control has a real command or is visibly unavailable.
- Page entry executes the correct refresh.
- Enabled/disabled state comes from authoritative state/capability.
- Success and errors are surfaced.
- Mutations refresh dependent state.
- No lifecycle/server/save implementation is duplicated in Avalonia code-behind.
- Local and remote profiles both behave correctly.
- Authentication/TLS behavior is unchanged.
- Destructive actions are guarded.
- Logic tests cover each functional mapping.

## Current API route inventory

- `GET /healthz`
- `GET /api/v1/status`
- `GET /api/v1/status/poll`
- `GET /api/v1/service`
- `GET /api/v1/players`
- `GET /api/v1/logs/tail`
- `GET /api/v1/activity/tail`
- `POST /api/v1/players/{playerId}/action`
- `GET /api/v1/metrics`
- `GET /api/v1/doctor`
- `GET /api/v1/diagnostics/network`
- `POST /api/v1/diagnostics/network/firewall/repair`
- `POST /api/v1/diagnostics/network/restart`
- `GET /api/v1/world/explorer`
- `GET /api/v1/world/players-guilds`
- `GET /api/v1/mods`
- `GET /api/v1/mods/verify`
- `GET /api/v1/ue4ss`
- `POST /api/v1/mods/{type}/{package}/enabled`
- `GET /api/v1/server/distribution`
- `GET /api/v1/server/distribution/plan`
- `POST /api/v1/server/distribution/update`
- `GET /api/v1/backups`
- `POST /api/v1/backups/create`
- `DELETE /api/v1/backups/{fileName}`
- `POST /api/v1/backups/{fileName}/restore`
- `GET /api/v1/palworld/config`
- `PUT /api/v1/palworld/config`
- `GET /api/v1/config/editable`
- `PUT /api/v1/config/editable`
- `GET /api/v1/config`
- `POST /api/v1/server/start`
- `POST /api/v1/server/stop`
- `POST /api/v1/server/restart`

## Companion files

- `MystTiq_v0.2.16.4_GUI_Event_Inventory.csv`: all **287** extracted historical GUI event bindings.
- `MystTiq_GUI_Functional_Crosswalk_v0.2.16.4_to_v0.4.6.3.csv`: prioritized old→current workflow mapping.

## Versioning recommendation

Keep GUI restoration incremental. First restore visual parity around already-supported operations, then restore client-only filters/exports, then add missing backend capabilities one bounded area at a time. Reserve final fix-level revisions for syntax, styling and logic-test corrections rather than mixing unrelated new functionality.