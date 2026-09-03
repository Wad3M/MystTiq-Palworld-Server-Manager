# MystTiq Palworld Server Manager
> v0.4.5.1 compile correction: the Palworld configuration ListBox uses supported Avalonia `ListBoxItem` stretching; `HorizontalContentAlignment` is not applied to `ListBox`.


## Supplied source reference: v0.4.17.4

v0.4.17.4 remains the user-supplied FullSource reference. It is retained for comparison and was not installed over the newer working tree.

## Functional baseline: v0.4.18.2

v0.4.18.2 is the accepted installed functional baseline. Its headless-first routes, single aggregate status poll, Windows/Linux Avalonia targets, remote/LAN security boundary, card containment, and 287 classified legacy GUI handlers are carried forward unchanged.

## Current release candidate: v0.6.0.0 — Architecture Baseline & Operation Platform

v0.6.0.0 is the first milestone of the `v0.6.x.0` family (`docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md`), building on the completed v0.5.1.x–v0.5.5.0 shell-integration line. It adds a real Operation/Provider/Health/Transaction contract set in `MystTiq.Core/Operations/` — `OperationId`/`OperationRecord`/`OperationPhase`/`OperationTransactionState`, `ServerProfileId`, `ServerHealthState`, `ICapabilityProvider`, and a central `IOperationCoordinator` — plus a resource-key lock table that rejects any operation whose keys overlap one already in flight, naming the blocking operation in its message. The three existing destructive-mutation services (World Transactions, Guild Ownership, Base Ownership/Recovery) are migrated onto it by wrapping their proven per-feature journals rather than replacing them, all sharing resource key `world-mutation` since they mutate the same `Level.sav` — so the coordinator now correctly rejects any of them running concurrently with another, a real safety property that did not exist before. New `GET /api/v1/operations`/`GET /api/v1/operations/{id}` routes back a new "Operation Platform" card on the Desktop World Transactions page. This pass is deliberately scoped down from the milestone's full acceptance checklist — priority queueing, dependency-graph blocking, restart-safe reclassification, SteamCMD phase parsing, and a full log-tail UI are explicitly deferred, not silently dropped; see `docs/architecture/v0.6.0.0-operation-platform.md`.

## v0.5.5.0 — Desktop Shell UI/UX Consolidation

v0.5.5.0 is a Desktop-only visual/UX pass with no backend or API changes. The sidebar navigation `ToggleButton` template is replaced with a bare `ContentPresenter`, because FluentTheme's default template was painting its own checked/pointerover background directly on an internal part that a plain `Background` setter could not override — this was bleeding a solid `#0078D4` default-blue frame around the custom rounded glass pill. Nav items are taller (58px) and the sidebar is wider (260px); the pill now stretches to its full row height instead of being centered at its natural text height (fixed via `VerticalContentAlignment="Stretch"` plus an explicit `VerticalAlignment="Center"` on the label); hovering an already-selected item now visibly brightens instead of showing zero change; and glow effects use `DropShadowEffect` (which follows the rendered rounded silhouette) instead of `BoxShadow` (which was painting a hard rectangular corner past the `CornerRadius`). The Dashboard SERVER card is now a clean three-state traffic light (red = not running, amber = transitioning, green = running) instead of a fourth neutral-grey state that made a stopped server look calm rather than stopped, and the OVERALL HEALTH label color now follows the same state as its card instead of staying hardcoded green through an ATTENTION/red state. Every page's previously-duplicated title banner (18 of them, e.g. "Server Setup", "Backup Center", "MOD Dashboard") was removed from the page body now that the page title/subtitle renders once in a shared card in the ribbon row, which also now carries the Auto refresh checkbox and Connected badge — freeing meaningful vertical space on every page.

## v0.5.4.0 — Base Recovery

v0.5.4.0 completed the v0.5.x Base ownership-repair work by porting the legacy app's `DeleteBaseAndOwnedObjects` operation ("Recover Base") onto the same transaction pattern used by Base Ownership Transfer. `HeadlessBaseOwnershipService` gained a Preview/Apply Recover pair that removes the base's `base_ids` entry from its owning guild and deletes every structure/container/worker record tagged to it outright, rather than reassigning them. Removal only ever discards whole array elements (never a keyed object property), because some record shapes (e.g. `MapObjectSaveData`) carry the ownership tag several levels below the record's own array element — deleting the inner key directly would leave a structurally required object empty and corrupt the save encoder. Preview decodes the save up front and reports exactly how many owned records will be permanently removed before the user confirms, since this operation is destructive beyond what a safety backup restore can casually undo.

Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS, 192.168.1.144) against a real guild/base-populated save (copied into an isolated test environment, never against live/production data): Preview correctly reported 132 tagged records for the test base, Apply removed all 132 and the `base_ids` entry, and the result was independently re-decoded (not trusting the service's own report) to confirm zero remaining references and a correct `base_ids` array on the owning guild — identical outcome on both platforms. The Bases page's "Recover Base — BACKEND REQUIRED" stub is now a real Preview/Apply card with a destructive-action confirmation checkbox.

## v0.5.3.0 — Base Ownership Transfer

v0.5.3.0 ports the ownership-repair pattern proven by v0.5.2.0's Guild Ownership platform onto Base. A real base's ownership is denormalized: it is not one field but a guild-level `base_ids` pointer plus a `group_id_belong_to` tag scattered across every structure/container/worker object placed at that base (confirmed against real save data: 132 separate tagged records for one base). `HeadlessBaseOwnershipService` moves the `base_ids` entry between guild records and retags every one of those objects to the new owning guild in a single transaction, following the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI pattern and journaling into the same transaction store. This release was scoped to Transfer Ownership only; Base Recovery followed in v0.5.4.0.

Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS, 192.168.1.144) against a real guild/base-populated save. On each platform: Preview correctly identified the source and target guilds, Apply reported 113 owning-guild tags updated, and the result was independently re-decoded (not trusting the service's own report) to confirm both guilds' `base_ids` arrays and all 113/113 retagged records — identical outcome on both platforms.

## v0.5.2.0 — Guild Ownership Platform

v0.5.2.0 adds the first slice of a real Guild/Base ownership-repair platform, replacing three previously-disabled "BACKEND REQUIRED" stub buttons on the Guilds page with working functionality: Claim Orphaned Guild, Transfer Leadership, and Add Player to Guild. All three share one Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation, the same pattern already proven by World Transactions, and journal into the same transaction store so existing Transaction Center history picks them up automatically. A new server-side save codec (`HeadlessSaveCodecService`) detects each save's actual container format (plain PlZ vs. PlM/Oodle) per file rather than assuming one converter — this was found to matter immediately: the reference Linux deployment's real save uses the PlM/Oodle container. Base ownership/Palbox repair remains BACKEND REQUIRED; the pattern now exists and is proven, it just hasn't been ported onto Base's different data shape yet.

Two additional fixes landed alongside it: `PalworldSettingsConfigurationService.Load()` no longer crashes the entire `/api/v1/status/poll` aggregate endpoint when `PalWorldSettings.ini` exists but hasn't been fully written yet (a real state observed on a freshly-started PalServer); and both the headless CLI's `--help` output and the desktop's displayed version now derive from the assembly instead of a hardcoded string literal, closing a gap where the CLI had silently displayed the wrong version for six releases.

Verified end-to-end against a real Linux deployment (Ubuntu 24.04.4 LTS, matching the documented reference environment) via `scripts/Deploy-Test-MystTiqLinux.ps1`, not just compiled for `linux-x64`. The PlM/Oodle converter's C++ Oodle bindings were built from source on Linux for this verification and compiled cleanly. Transfer Leadership and Add Player to Guild were run to completion against a copy of a real guild-populated save, with the result independently re-decoded (not trusting the service's own report) to confirm the change actually persisted — same outcome both times as the Windows verification. Claim Orphaned Guild's rejection path (correctly declining a non-orphaned guild) was verified on both platforms; its apply path was not, since no orphaned-guild test data existed on either platform. The `status/poll` fix was confirmed live on the real Linux service.

## v0.5.1.5 — First-Run Setup Wiring and Update Center Correction

v0.5.1.5 restores the Server Setup first-run defaults form for server identity, optional passwords, player capacity, and game/REST ports. Creation follows View → ViewModel → authenticated API client → headless route → configuration service, requires explicit confirmation, refuses to overwrite an active configuration, preserves the official REST/RCON enablement defaults, and omits sensitive values from audit details. It also restores the Update Center platform, SteamCMD, and PalServer summary cards to their correct page.

Prototype demo commands were not carried over. Refresh, lifecycle, backup, console, Doctor, navigation and page actions remain connected through View → ViewModel → API client → route → headless service. Home contains Dashboard; World contains Inspector, Players, Bases and Guilds; System contains Settings, Notifications, and Activity & Audit.

Global card containment, centered metallic buttons, and guarded close-before-relaunch remain shared desktop behavior.

v0.4.5.1 also restores the interaction layer expected from the legacy GUI: standard hover information tooltips, right-click player administration, persistent Activity/Audit logs, PalServer console capture when MystTiq launches the Windows server, and stricter canonical-world discovery so backup snapshots are not counted as live worlds. Kick and Ban are routed through Palworld REST. Whisper, Promote and Give Item remain capability-gated unless a supported server/mod provider exists; MystTiq does not fake unsupported vanilla operations.

The World Inspector now preserves the full World ID, adds a compact nickname for readability, truncates only non-authoritative display paths, and scans canonical SaveGames world directories instead of recursively accepting any backup copy containing `Level.sav`.

Closing the main window now hides MystTiq to the system tray instead of abandoning a background sidecar with no UI control. The tray can reopen the GUI, issue PalServer Start/Restart/Stop through the ViewModel/API path, stop only the sidecar started by the current GUI session, or exit the GUI while intentionally leaving the backend running. Remote/LAN authentication and TLS behavior remain unchanged.



### GUI reconstruction authority and roadmap

Before restoring a legacy page, use `docs/reconstruction/MystTiq_GUI_Reconstruction_Guide.md` and follow the mandatory chain **old visual/control → old handler intent → current ViewModel command → current API client → current API route → current headless service**. Missing state-changing behavior is **BACKEND REQUIRED**, never a decorative no-op button. Dangerous world/save mutations must follow **Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI**.

The versioned implementation plan is `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md`.

### Promoted v0.4.6.8 dashboard containment fix

The compact CPU/Memory utilization meters are clipped and proportionally constrained inside their status card. The custom Resource History chart also clips its DrawingContext and insets plotted strokes so antialiasing cannot render beyond the chart/card boundary.

### v0.4.8.0 Configuration parity

- Restores v0.2.16.4-style **Simple Settings** and **Advanced Settings** views over the same authoritative Palworld setting DTOs.
- Adds local search and category filtering without adding backend polling or duplicating server logic.
- Restores **Official / Vanilla**, **Balanced QoL**, and **Relaxed QoL** presets using the historical v0.2.16.4 preset values; server identity/password/port fields are not changed by QoL presets.
- Adds dirty-state and client-side validation for key port/max-player fields; Save Changes remains API-routed and rollback-backed.
- Adds cross-platform Avalonia file-picker **Import/Export**. Import only updates the local editor; it never writes directly to a server path and requires Save Changes to persist through the management API.
- Preserves unknown/custom OptionSettings returned by the headless service.


### v0.4.9.1 Console + RCON parity

- Restores severity/category/search filtering, Hide routine REST, Pause/Resume, Clear View, Refresh View and local Export around the combined green console stream.
- Adds `/api/v1/rcon/status`, `/api/v1/rcon/doctor`, and `/api/v1/rcon/command` through a Core-owned Source RCON client.
- Reads RCONEnabled/RCONPort/AdminPassword only inside the headless service. AdminPassword is never serialized to the desktop.
- Connect validates configuration, TCP reachability and authentication; Disconnect clears GUI connection state because command requests use short-lived server-side RCON sessions.
- Includes presets for Info, ShowPlayers, Save, Broadcast and graceful Shutdown.
- Preserves the existing REST-backed administration and marks RCON as legacy/deprecated rather than making it the primary management API.
- Applies global card containment so dynamic UI content cannot render beyond card boundaries.

### v0.4.10.0 Players parity

- Combines live REST sessions and server-side save discovery into one stable-ID player directory.
- Restores player search, view/admin filters, selected detail/evidence, save discovery and local CSV export.
- Adds server-owned notes and warnings with bounded input, atomic persistence, and privacy-conscious mutation audits.
- Enables Kick/Ban only for live players; unsupported provider and whitelist/temporary-ban/unban/admin-removal actions remain visibly unavailable.
- Centers all standard button content and adds the shared metallic visual treatment.
- Stops only artifact-hosted MystTiq desktop/sidecar processes before a build relaunch.

### v0.4.11.0 Guilds + Bases parity

- Restores guild search/status filters, directory/detail panes, Copy Guild ID, Open Leader Player, member/base evidence, and CSV export.
- Restores base search/status filters, ownership evidence/detail, Copy Base ID, and CSV export.
- Extends the headless DTO with authoritative decoded `base_ids`; the GUI never parses or opens remote save files.
- Keeps transfer/claim/repair/recovery controls visibly BACKEND REQUIRED until the mandatory transactional safety chain exists.

### v0.4.12.0 Backup Center parity

- Adds deep selected/all ZIP verification, SHA-256 evidence, persisted verification records, and privacy-safe audit summaries.
- Requires explicit restore confirmation while preserving stopped-server, pre-restore safety backup, staging, and rollback semantics.
- Adds server-side retention preview/apply with single-use expiring tokens and inventory-change rejection.
- Opens the backup root only for the local profile; remote profiles keep all operations on the headless server.

### v0.4.13.0 MOD Dashboard / MOD Library / UE4SS parity

- Adds staged, size-bounded, traversal-safe PAK and UE4SS MOD ZIP installation through the API.
- Adds selected delete, enable/disable all, and authoritative mods.txt repair with privacy-safe audits.
- Retains neutral Disabled and Active / Unverified health semantics and server-side runtime evidence.
- Keeps Workshop import, legacy migration, and UE4SS runtime-wide install/backup/restore visibly BACKEND REQUIRED.

### v0.4.14.0 World Inspector read-only parity

- Restores Overview, Players, Guilds, Bases, Saves, Statistics, Files, World Explorer, World Health, and Integrity sections.
- Adds server-side file-category statistics and structural integrity findings without decoding or mutating saves.
- Preserves canonical-only active-world discovery, full World IDs, and remote-safe API inspection.

### v0.4.15.0 World Validator, Recovery & Transaction Center

- Restores Validate Active World, findings, local report export, Repair/Recovery plan review, and durable Transaction Center history.
- Adds remote-safe ZIP Analyze → Review Plan → Confirmed Apply for full-world import and canonical player-save recovery.
- Requires stopped PalServer and a fresh safety backup before staging, atomic swap, post-validation, journal/audit, and GUI refresh.
- Rejects unsafe, oversized, over-expanded, or structurally invalid archives and has failure-injection coverage for preview, backup, staging, swap, and validation.

### v0.4.16.0 Activity & Audit + Notifications parity

- Adds client-side search plus severity/category filtering over persistent server activity evidence and local export of the visible view.
- Keeps persistent audit append-only from the GUI; no silent clear endpoint is exposed.
- Adds a server-side bounded, atomic notification store with unread and pinned counts.
- Restores the notification bell/page, search/severity filtering, mark read/unread, pin/unpin, dismiss, mark-all-read, semantic self-test and export.

### v0.4.6.8 latest FullSource deployment helper

On Windows, `Update-FromDownloads.ps1` selects the most recently modified `MystTiqPalworldServer_v*_FullSource*.zip` from Downloads, validates and stages it before touching the installed tree, runs the installed `Build.ps1 Clean`, clears `C:\GameServers\MystTiqPalLinux`, installs the staged source, unblocks scripts, then runs `Test-CurrentRelease.ps1`. A successful update therefore continues automatically through Clean → strict Validate → current-version logic tests → Windows/Linux builds → runtime smoke. Use `-SkipBuild` only when intentionally staging source without the full gate.

```powershell
cd C:\GameServers\MystTiqPalLinux
.\Update-FromDownloads.ps1
```

The updater copies its destructive helper into the Windows temp directory before replacement so clearing the source tree cannot invalidate the running updater. The default path is now the full build/test gate; if any stage fails, PowerShell stops and reports the failure rather than silently continuing.

### v0.4.6.8 Resource History and runtime metrics parity

Resource History now mirrors the legacy dashboard information architecture: CPU and memory are plotted as separate live lines, the range selector supports **1 Hour / 6 Hours / 24 Hours / 7 Days / 30 Days**, and the left summary shows average/peak values plus the sample count. Historical telemetry is persisted by the headless backend with 30-day retention. It is recorded from the existing aggregate status request, so no competing periodic polling loop is introduced. Range changes use an on-demand `/api/v1/history` request.

Runtime metrics now aggregate the managed PalServer process set rather than trusting one PID. This keeps CPU/RAM/thread values correct when `PalServer.exe` is only a bootstrap process and `PalServer-Win64-Shipping-Cmd.exe` owns most of the runtime work.

Server Setup now mirrors the v0.2.16.4 information architecture: **Environment Health**, a scrollable **Action / Component / Status / Location / Details** checklist, **Check for Updates / Install Missing / Verify Files**, and an **Operation Monitor** with progress. The checklist comes from the headless service so the same prerequisite evidence is available to Windows and Linux clients. Missing SteamCMD can be provisioned from Valve's configured package URI before the Palworld Dedicated Server update/install operation.

Navigation selection is now state-driven. Clicking a destination or navigating there from another page updates the highlighted child item and expands the appropriate parent group automatically.

### v0.4.6.8 window suppression and dashboard changes

- Windows PalServer starts without a visible Unreal logging console.
- Default Windows launch replaces `-log` with `-stdout -FullStdOutLogOutput`, while keeping `-logformat=text`.
- stdout/stderr are captured asynchronously into `MystTiq-PalServer-Console.log`.
- The Live Console API/UI prioritizes that redirected stream and falls back to Palworld's file log only when necessary.
- Stop/restart/tray behavior remains owned by the headless lifecycle service; hiding the server window does not detach process management.

### v0.4.6.8 connection/session changes

The desktop now verifies `/healthz` identity, API contract version, backend version, platform and authentication state before declaring a management session usable. Initial connection and post-lifecycle refresh use the same aggregate `/api/v1/status/poll` contract used by the status bar. A desktop-owned sidecar is forced to loopback-only unauthenticated mode; remote/LAN APIs still require their configured bearer/TLS security. If the usual loopback port is occupied by an older/incompatible MystTiq process, the GUI launches its packaged sidecar on a private available loopback port and connects to that exact endpoint instead of treating any health response as compatible. The Windows launch path preserves the established performance arguments but suppresses Unreal's separate `-log` window, using redirected stdout plus full log output so PalServer remains managed inside MystTiq.


Versioning follows `MAJOR.MINOR.REVISION.FIX`. New revisions start at `.0`; fixes increment only the final component. v0.4.18.2 is the completed restoration baseline, v0.5.1.0 starts functional visual-shell integration, v0.5.1.2 integrates the actual v0.5 prototype shell, v0.5.1.5 carries first-run setup wiring forward, v0.5.2.0 starts the Guild Ownership platform, v0.5.3.0 extends it to Base Ownership Transfer, v0.5.4.0 completes it with Base Recovery, v0.5.5.0 is a Desktop shell UI/UX consolidation pass, and v0.6.0.0 starts the `v0.6.x.0` family with the Operation Platform architecture baseline. Historical release labels remain historical records.

Current candidate logic test:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.6.0.0-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

The equivalent version-aware wrapper is:

```powershell
.\scripts\Test-CurrentRelease.ps1 -ProjectRoot . -ExportJson
```

`Test-CurrentRelease.ps1` resolves the version from `Directory.Build.props`, verifies the matching logic-test file exists, unblocks scripts, runs Clean, runs strict Validate, then executes that version's logic test with its build gate.

`Build.ps1 Clean` also stops only auto-launched MystTiq development processes whose executable is under this repository's `artifacts` tree before deleting it. It never stops PalServer or an installed MystTiq service outside the development publish tree, and Clean now fails explicitly if `artifacts` cannot be removed.


### GUI launch after compile

`Build.ps1 DesktopWindows` now launches the published Avalonia GUI automatically only after a successful Windows desktop publish. Use `-NoGuiLaunch` for CI or unattended builds. Release packaging suppresses auto-launch automatically.

### GUI parity reference

The cross-platform GUI is audited against the final v0.2.16.4 Windows WPF release in `docs/GUI_PARITY_v0.5.3.0.md`; the ordered screen/control comparison is in `docs/GUI_SCREEN_COMPARISON_v0.5.3.0.md`; all 287 historical handlers remain classified in `docs/GUI_EVENT_COVERAGE_v0.5.3.0.md` and its row-level CSV. Navigation labels alone do not count as parity.

- v0.4.6.8 connection contract fix: desktop lifecycle DTO now accepts nullable `lastTransitionAt`, matching the headless/Core wire model so a stopped server can establish the management session before its first lifecycle transition.

### v0.4.6.8 application exit policy
The tray exposes three explicit exit choices: **Safe Exit** gracefully stops PalServer and then the GUI-owned local management backend; **Force Exit** immediately escalates PalServer shutdown through the lifecycle service and then terminates the GUI-owned backend; **Exit GUI Only** closes MystTiq while intentionally leaving services running. Safe Exit should be the normal choice. Force Exit is intended for a hung server or shutdown path.
