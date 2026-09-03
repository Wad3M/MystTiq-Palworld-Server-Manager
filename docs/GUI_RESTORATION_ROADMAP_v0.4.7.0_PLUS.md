# MystTiq GUI Restoration Roadmap — v0.4.7.0+

## Authority and reconstruction rule

This roadmap is derived from the supplied `MystTiq_GUI_Reconstruction_Guide`, the 287-event v0.2.16.4 GUI inventory, and the v0.2.16.4 → current functional crosswalk. The historical v0.2.16.4 GUI is the visual/workflow authority; the current Avalonia + headless/API architecture is the implementation authority.

Every restored operational control MUST be traced in this order:

**old visual/control → old handler intent → current ViewModel command → current API client → current API route → current headless service**

If a state-changing historical function has no safe current backend implementation, it MUST be labelled **BACKEND REQUIRED** in the implementation notes/tests and MUST NOT be represented by a button that silently does nothing. Do not copy old WPF local-only process/filesystem/save mutation code into Avalonia.

For dangerous world/save/guild/base/recovery mutations, the mandatory sequence is:

**Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI**

Each feature build uses `.0`; fixes to that feature build increment the final component. Promotion to the next feature build is blocked until validation, logic, regression, Windows/Linux build, packaging and runtime acceptance are green.

## Definition of complete for every restored page

A page is not complete because its title exists. It is complete only when its historical visual hierarchy is recognizably reproduced, selected navigation state follows the actual page, page entry triggers the correct refresh, every enabled operational control has real wiring, unsupported controls visibly explain why they are unavailable, errors/success are surfaced, mutations refresh dependent state, remote/LAN/TLS behavior is preserved, and logic tests trace functional controls through the complete architecture.

---

## v0.4.7.0 — Dashboard + Server Setup parity closeout

### Historical sections to reproduce
- Dashboard top lifecycle/health cards, World Pulse, Health strip, Resource History, Live Activity, Live Server/Manager Log, Online Players, Server Name/Description footer.
- Server Setup `SERVER ENVIRONMENT` checklist, Environment Health, Server Operations, Operation Monitor.

### Controls/workflows
| Historical control | Old handler intent | Current command | API client / route | Headless owner | Classification |
|---|---|---|---|---|---|
| START | `Start_Click` | `StartCommand` | `StartServerAsync` → `POST /api/v1/server/start` | lifecycle service/platform | REWIRE |
| RESTART | `Restart_Click` | `RestartCommand` | `RestartServerAsync` → `POST /api/v1/server/restart` | lifecycle service/platform | REWIRE |
| STOP | `Stop_Click` | `StopCommand` | `StopServerAsync` → `POST /api/v1/server/stop` | lifecycle service/platform | REWIRE |
| BACKUP | `Backup_Click` | `CreateBackupCommand` | `CreateBackupAsync` → `POST /api/v1/backups/create` | backup service | REWIRE |
| DOCTOR | `DashboardOpenDoctor_Click` | navigation / page refresh | `GET /api/v1/doctor` | doctor service | REWIRE |
| Update / Verify / Install | `Update_Click`, `VerifyEnvironment_Click`, `InstallRequired_Click` | distribution commands | distribution GET/plan/update routes | server distribution service | ADAPT |

### Logic/runtime tests required
- Dashboard surfaces all consume the single aggregate poll; no second periodic timer.
- CPU/RAM history range selection is on-demand only.
- Server Setup environment rows are backend-derived, not hardcoded samples.
- Every actionable environment row has a real command or a visible unavailable reason.
- Selected navigation highlight follows Dashboard/Server Setup including programmatic navigation.
- Runtime: start/stop/restart, hidden PalServer window, live console, UDP readiness, dashboard live updates.

---

## v0.4.8.0 — Configuration parity

### Historical sections to reproduce
- Simple Settings / Advanced Settings modes.
- Search box and category filtering.
- Server identity fields and generated server-name helper.
- QoL/preset surface where historically present.
- Import/export configuration.
- Save Changes with dirty-state indication and validation summary.

### Controls/workflows
| Historical control | Old handler | Current mapping | Classification |
|---|---|---|---|
| `SaveConfigButton` / SAVE CHANGES | `SaveConfig_Click` | `SavePalworldConfigurationCommand` → `PUT /api/v1/palworld/config` | REWIRE |
| SIMPLE SETTINGS | `ShowSimpleConfig_Click` | local presentation over current setting DTOs | REIMPLEMENT client-only |
| ADVANCED SETTINGS | `ShowAdvancedConfig_Click` | local presentation over current setting DTOs | REIMPLEMENT client-only |
| `ConfigSearchBox` | `ConfigFilter_Changed` | local filtering over current setting DTOs | REIMPLEMENT client-only |
| `ConfigCategoryCombo` | `ConfigFilter_Changed` | local filtering over current setting DTOs | REIMPLEMENT client-only |
| Import / Export | `ImportConfig_Click` / `ExportConfig_Click` | safe client/server file exchange design | ADAPT; BACKEND REQUIRED where remote file access is needed |

### Tests required
- In-memory filtering/category tests.
- Save traces View → ViewModel → API → route → Core and creates rollback backup.
- Local and remote profiles behave consistently.
- Unknown/custom Palworld keys survive round-trip.
- Import never writes directly to remote server paths from GUI.

---

## v0.4.9.0 — Console + RCON parity

### Historical sections to reproduce
- Green live console with severity/category/search filters.
- Hide REST traffic toggle.
- Refresh, Pause, Clear View, Export View.
- RCON status, doctor/connect/disconnect/send/history if historical workflow is restored.

### Controls/workflows
| Historical control | Old handler | Current mapping | Classification |
|---|---|---|---|
| Refresh Console | `RefreshConsole_Click` | aggregate poll/log tail | REWIRE |
| Severity/category filters | `ConsoleFilter_Changed` | client-side filtered projection | REIMPLEMENT client-only |
| Search | `ConsoleSearch_Changed` | client-side | REIMPLEMENT client-only |
| Pause | `PauseConsole_Click` | pause presentation, not backend capture | REIMPLEMENT client-only |
| Clear View | `ClearConsole_Click` | clear visible buffer only | REIMPLEMENT client-only |
| Export | `SaveVisibleLog_Click` | client-side export | REIMPLEMENT client-only |
| RCON doctor/connect/send | historical RCON handlers | `RconDoctorCommand/RconConnectCommand/RconSendCommand` → MystTiq API → Core Source RCON service | REIMPLEMENT server-side |

### Tests required
- Filters do not alter persistent logs.
- Pause stops UI append but backend capture continues.
- Export contains exactly visible filtered rows.
- RCON status/doctor/send execute through the headless API and Core Source RCON service.
- AdminPassword remains server-side; the GUI owns no RCON socket/credential.
- Timeout/error tests and audit entries are required.
- RCON is labelled legacy/deprecated while REST/headless APIs remain preferred.

---

## v0.4.10.0 — Players parity

### Historical sections to reproduce
- Search/view/admin filters, live/known player grid, selected-player details, save discovery, export CSV, notes/warnings/whitelist administration.
- Right-click menu remains supported.

### Controls/workflows
- Refresh → `RefreshMonitoringCommand` → players/status API: REWIRE.
- Kick/Ban → current player-admin action API: REWIRE.
- Whisper/Promote/Give Item → capability-gated: KEEP DISABLED/EXPLAIN unless provider added.
- Discover saves / export CSV: client/headless adaptation depending remote path needs.
- Temp ban/unban/admin removal/whitelist/notes/warnings: **BACKEND REQUIRED** unless data is purely client-side and non-authoritative.

### Tests required
- Selected player survives refresh by stable ID where possible.
- Context menu CanExecute follows live/capability state.
- Unsupported operations cannot produce fake success.
- Every mutation is audited.

---

## v0.4.11.0 — Guilds + Bases parity

### Historical sections to reproduce
- Guild search/status filters, export, grid, detail pane, Copy Guild ID, Open Player.
- Base ownership/evidence views and repair/recovery planning surfaces.

### Current safe mapping
- Read-only discovery → `RefreshPlayerGuildExplorerCommand` → `GET /api/v1/world/players-guilds`: REWIRE/ADAPT.
- Add player, transfer leader, claim orphan, repair mappings, ownership apply: **BACKEND REQUIRED**.

### Mandatory mutation contract
All guild/base mutations: **Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI**.

### Tests required
- Read-only view uses decoded authoritative data.
- Mutation buttons cannot enable unless preview capability and backend route exist.
- Transaction failure restores/retains backup and journals result.

---

## v0.4.12.0 — Backup Center parity

### Historical sections to reproduce
- Create, refresh, restore, delete.
- Verify Selected, Verify All.
- Retention Preview / Apply Cleanup.
- Open Backup Root with remote-safe behavior.

### Mapping
- Create/refresh/restore/delete → existing backup API: REWIRE.
- Verification and retention → **BACKEND REQUIRED** for authoritative remote-safe execution.
- Open root → local shell only for local profile; remote profile offers path copy/server-side listing rather than opening a local folder.

### Tests required
- Restore requires confirmation and safety semantics.
- Retention preview is immutable; apply executes exactly previewed policy or requires re-preview.
- Backup verification results are persisted/audited.

---

## v0.4.13.0 — MOD Dashboard / MOD Library / UE4SS parity

### Historical sections to reproduce
- MOD dashboard verification/report/details.
- MOD Library install ZIP, delete, enable/disable selected/all, repair, legacy migration, Steam import, web/workshop information.
- UE4SS version/source/releases/install/import/backup/restore/toggle.

### Mapping
- Refresh/verify/enable/disable → current MOD API: REWIRE.
- Install/delete/all/repair/import/workshop → **BACKEND REQUIRED**.
- UE4SS health → current endpoint: REWIRE.
- UE4SS mutation/version management → **BACKEND REQUIRED**.

### Tests required
- Disabled/Active-Unverified neutral-health semantics preserved.
- ZIP install path traversal protection.
- MOD/UE4SS mutations are server-side and audited.
- Remote GUI never opens server-local MOD paths directly.

---

## v0.4.14.0 — World Inspector read-only parity

### Historical sections to reproduce
- Overview, Players, Guilds, Bases, Saves, Statistics, Files, World Explorer, World Health, Integrity.
- Full canonical World ID + nickname and copy action.
- Active-world folder/file hierarchy with backup copies excluded.

### Mapping
- Active world inspection → `RefreshWorldExplorerCommand` → `GET /api/v1/world/explorer`: REWIRE.
- Player/guild evidence → current decoded explorer routes: REWIRE.
- Additional integrity/statistics/file metadata endpoints may be **BACKEND REQUIRED** if current DTOs do not carry needed evidence.

### Tests required
- Never classify backup snapshots as active worlds.
- Full World ID never truncates; nickname is presentation-only.
- Remote-safe file inspection; no direct GUI filesystem assumptions.

---

## v0.4.15.0 — World Validator, Repair, Transaction Center + Recovery

### Historical sections to reproduce
- Validate Active World, findings grid, export report.
- Repair Center / transaction plan / backup links / journal.
- Player Recovery and Guild/Base Recovery workflows.
- Import archive Analyze → Accept Options → Review Plan → Apply.

### Classification
All state-changing functionality in this release is **BACKEND REQUIRED** until the complete headless transaction service exists.

### Mandatory workflow
**Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI**.

### Tests required
- Preview is side-effect free.
- No apply without fresh safety backup.
- Transaction is atomic or has explicit rollback/recovery journal.
- Validation executes after mutation before success is reported.
- Failure injection tests for each stage.
- Remote/LAN behavior and authentication preserved.

---

## v0.4.16.0 — Activity & Audit + Notifications parity

### Historical sections to reproduce
- Activity search/severity/category filtering, export, clear policy.
- Notification bell/flyout, mark read, pin/dismiss, search, self-test, export.

### Mapping
- Activity tail → existing activity endpoint: REWIRE.
- Activity filtering/export → client-only REIMPLEMENT.
- Persistent audit clear → policy-controlled **BACKEND REQUIRED** if offered.
- Notifications → **BACKEND REQUIRED** for persistent authoritative subsystem; client-only flyout presentation after service exists.

### Tests required
- Audit records management mutations and cannot be silently erased.
- Polling noise excluded.
- Notification read/pin/dismiss state persists if advertised as persistent.

---

## v0.4.17.0 — Crash Analyzer + Palworld Save Tools parity

### Historical sections to reproduce
- Crash history/stability/isolation controls.
- Save Tools self-tests, Python/converter/Oodle checks, save diagnostics, browse/inspect/export.

### Classification
- Existing placeholders are not completion.
- Crash analysis data collection/service: **BACKEND REQUIRED** where server-side logs/files are needed.
- Save decode/tool tests: **BACKEND REQUIRED** for remote-safe execution; client-only rendering may remain in Avalonia.

### Tests required
- Crash records correlate lifecycle exit + logs without inventing causes.
- Save inspection read-only tests before any mutation capability.
- Tool invocation paths are platform abstractions for Windows/Linux.

---

## v0.4.18.0 — Workspace + Diagnostics + Settings final parity and closeout

### Historical sections to reproduce/preserve
- Workspace refresh/validate/open/browse/save paths.
- Diagnostics full report, firewall/listener/network checks, export/support package/copy/open diagnostics.
- Settings retains the newer connection profiles, LAN discovery, bearer token, TLS/certificate pin and local bootstrap behavior.

### Mapping
- Workspace load/save → current editable configuration API: ADAPT.
- Network diagnostics/firewall repair/restart → current diagnostics API: REUSE.
- Export/support package → ADAPT / **BACKEND REQUIRED** for server-side artifact collection.
- Settings connection/auth/TLS → KEEP NEW.

### Tests required
- Local browse/open controls hidden or adapted for remote profiles.
- Support package contains intended diagnostics and redacts secrets.
- TLS/bearer token/certificate pin behavior regression tests.
- Final 287-event inventory coverage report: every historical handler classified and either mapped, intentionally obsolete, visibly unavailable, or BACKEND REQUIRED with roadmap closure evidence.

---

## Per-version logic-test template

Every restoration build must add/extend `Test-vX.X.X.X-Logic.ps1` with all applicable checks:

1. **Visual structure:** historical sections/controls exist with recognizable hierarchy.
2. **Navigation state:** selected destination is highlighted and parent expands on programmatic navigation.
3. **Composition:** View → ViewModel → API client → route → headless service → Core/platform where applicable.
4. **Behavioral/in-memory:** inject known backend state, execute command, verify UI state/CanExecute/result.
5. **Capability truth:** missing backend = visibly unavailable/BACKEND REQUIRED, never fake success.
6. **Mutation safety:** Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI.
7. **Remote safety:** no local filesystem/process assumption for remote profiles.
8. **Security:** authentication/TLS/certificate pinning preserved.
9. **Regression:** single periodic polling path preserved; Windows/Linux builds preserved.
10. **Documentation/version:** roadmap, README, CHANGELOG, test names, release notes and manifest agree.

## Reference files shipped with source

- `docs/reconstruction/MystTiq_GUI_Reconstruction_Guide.md`
- `docs/reconstruction/MystTiq_v0.2.16.4_GUI_Event_Inventory.csv`
- `docs/reconstruction/MystTiq_GUI_Functional_Crosswalk_v0.2.16.4_to_v0.4.6.3.csv`

These are the first references to give a coding AI before reconstructing a GUI section.
