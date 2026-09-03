## v0.6.0.0 — Architecture Baseline & Operation Platform

- Adds a real Operation/Provider/Health/Transaction contract set in `MystTiq.Core/Operations/`: `OperationId`/`OperationRecord`/`OperationPhase`/`OperationTransactionState`, `ServerProfileId`, `ServerHealthState`/`ServerHealthSnapshot`, `ICapabilityProvider`, and `IOperationCoordinator`/`OperationCoordinator` — first milestone of the `v0.6.x.0` family, laying the foundation the rest of it builds on.
- `OperationCoordinator` rejects any operation whose resource keys overlap one already in flight (reject-if-conflicting, not queue-and-wait), naming the blocking operation in the message, and persists one JSON journal per operation.
- Migrates the three existing destructive-mutation services — World Transactions, Guild Ownership, Base Ownership/Recovery — onto the coordinator by "wrapping, not replacing" their proven per-feature journals, layering cross-feature resource-locking on top rather than ripping out already-shipped, verified logic. All four share resource key `world-mutation` (they all touch the same `Level.sav`), so the coordinator now correctly rejects any of them running concurrently with another — a real safety property that did not exist before.
- Adds `GET /api/v1/operations` and `GET /api/v1/operations/{id}`; the Desktop World Transactions page gains a second "Operation Platform" card listing Kind/Source/Phase/TransactionState across all three migrated features with its own Refresh command.
- Scoped-down pass by design: priority-ordered queueing, dependency-graph blocking beyond a single resource key, restart-safe reclassification of interrupted operations, SteamCMD phase parsing, and a full log-tail UI are explicitly deferred, not silently dropped — see `docs/architecture/v0.6.0.0-operation-platform.md`.
- Verified end-to-end on Windows: a real Base Recovery Apply and a concurrent World Transaction Apply were fired simultaneously against an isolated copy of a real guild/base-populated save; the loser was rejected with the coordinator's own message naming the held resource key, proving the new cross-feature lock rather than any pre-existing per-service guard. The new `/api/v1/operations` routes were also confirmed working end-to-end against the live Linux VM.

## v0.5.5.0 — Desktop Shell UI/UX Consolidation

- Replaces the sidebar `ToggleButton.nav` template with a bare `ContentPresenter`, fixing a bug where FluentTheme's default checked/pointerover background bled a solid `#0078D4` default-blue frame around the custom rounded glass pill (a plain `Background` setter cannot override a template's own internal state-specific paint).
- Nav items are taller (58px) and the sidebar is wider (260px); the pill now stretches to fill its full row height instead of centering at its natural text height and leaving dead space.
- Hovering an already-selected nav item now visibly brightens instead of showing zero change (checked background was silently winning over hover with equal cascade specificity).
- Selected/hover glow switched from `BoxShadow` to `DropShadowEffect`, since `BoxShadow` was painting a hard rectangular corner past the rounded `CornerRadius` silhouette.
- Dashboard SERVER card is now a clean three-state traffic light (red/amber/green) instead of a fourth neutral-grey state that made a stopped server look calm rather than stopped; OVERALL HEALTH label color now follows its card's state instead of staying hardcoded green.
- Every page's duplicated title banner (18 of them) removed from the page body now that the title/subtitle renders once in a shared card in the ribbon row, which also now carries Auto refresh/Connected — frees vertical space on every page.
- Desktop-only change; no backend/API/platform-specific code touched.

## v0.5.4.0 — Base Recovery

- Adds Base Recovery ("Recover Base"), porting the legacy app's `DeleteBaseAndOwnedObjects` operation onto `HeadlessBaseOwnershipService`: removes the base's `base_ids` entry from its owning guild and deletes every `base_camp_id_belong_to`-tagged structure/container/worker record outright, rather than reassigning them like Transfer Ownership.
- Uses the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation as Base Ownership Transfer. Preview decodes the save up front and reports the exact record count that will be permanently removed before Apply.
- Removal only ever discards whole array elements, never a keyed object property — some record shapes (`MapObjectSaveData`) carry the ownership tag nested under `Model.value.RawData.value`, several levels below the record's own array element; deleting that inner key directly corrupted the structure and crashed the save encoder during initial testing, fixed by scanning each array element's full subtree for the tag before removing the whole element.
- The Bases page's "Recover Base — BACKEND REQUIRED" stub is replaced with a real Preview/Apply card carrying an explicit destructive-action confirmation checkbox.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save: 132/132 tagged records and the `base_ids` entry correctly removed, confirmed via independent re-decode with zero remaining references, identical outcome on both platforms.

## v0.5.3.0 — Base Ownership Transfer

- Adds Base Ownership Transfer, porting the Guild Ownership pattern onto Base: moves a base's `base_ids` entry between guild records and retags every structure/container/worker object placed at that base (132 tagged records confirmed for one real base) to the new owning guild.
- Uses the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation and journals into the same store as Guild Ownership and World Transactions.
- Scoped to Transfer Ownership only; Base Recovery (legacy `DeleteBaseAndOwnedObjects`) is not ported and remains an honest disabled stub.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save: 113/113 owning-guild tags correctly retagged and both guilds' `base_ids` arrays confirmed correct via independent re-decode, identical outcome on both platforms.

## v0.5.2.0 — Guild Ownership Platform

- Adds real Claim Orphaned Guild, Transfer Leadership, and Add Player to Guild operations, replacing three disabled "BACKEND REQUIRED" stub buttons on the Guilds page.
- All three share one Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation and journal into the same store as World Transactions.
- Adds a server-side save codec that detects each save's real container format (plain PlZ vs. PlM/Oodle) per file instead of assuming one converter.
- Fixes `PalworldSettingsConfigurationService.Load()` crashing the entire `/api/v1/status/poll` endpoint when `PalWorldSettings.ini` exists but hasn't been fully written yet.
- Fixes two version-string literals that had silently drifted from the real build version (headless `--help`, Desktop `Version` property); both now derive from the assembly.
- Fixes a dead "Open Server Log" button in Diagnostics Center.
- Base ownership/Palbox repair remains BACKEND REQUIRED — the pattern now exists and is proven, it just hasn't been ported onto Base's data shape yet.
- Verified end-to-end against a real Linux deployment (Ubuntu 24.04.4 LTS), not just compiled for Linux.

## v0.5.1.5 — First-Run Setup Wiring and Update Center Correction

- Restores the Server Setup first-run defaults form for identity, optional passwords, player capacity, and game/REST ports.
- Adds a confirmation-gated, authenticated headless creation route that validates inputs, refuses overwrite, preserves official REST/RCON enablement defaults, and omits credentials from audit details.
- Restores the Update Center platform, SteamCMD, and PalServer summary cards to the Update Center page.
- Preserves the v0.5.1.4 server-tab, Configuration, Backup Center, Console, Workspace, containment, polling, security, and Windows/Linux parity corrections.

## v0.5.1.4 — Legacy Page Detail and Server Tab Correction

- Corrects the selected server tab’s contained glass styling and places the add-server control directly beside the profile tabs.
- Expands Server Setup with component, ready, and attention summaries above the authoritative environment checklist.
- Restores Configuration server identity, separate Simple slider/selection controls, and the Advanced default/active settings table while removing lifecycle/status duplication.
- Restores all primary Backup Center commands to the top row with archive verification summaries.
- Removes CPU, RAM, thread, and recent-metric cards from Console; console rows remain green and timestamps are normalized to the first field.
- Restores Workspace deployment mode, health, server discovery, and executable/application/server/download/export/log locations with profile-safe actions.
- Fixes the desktop publisher so the scoped artifact process is closed before output files are overwritten.

## v0.5.1.3 — Navigation and UE4SS Parity Correction

- Moves Notifications into System and keeps the notification bell routed to the same persistent API-backed page.
- Adds the top add-server control and opens a blank, unsaved connection profile in Settings until explicitly saved.
- Replaces redundant UE4SS Installed MODs content with installed runtime version, health/evidence, and fork selection.
- Adds server-side UE4SS version detection across the existing API DTO boundary without desktop filesystem access.
- Standardizes single-line TextBox and ComboBox dimensions against the legacy GUI and records the ordered v0.2.16.4 screen comparison.
- Preserves the headless-first wiring, single aggregate poll, secured remote/LAN boundary, card containment and shared Windows/Linux Avalonia implementation.

## v0.5.1.2 — v0.5 Shell Functional Integration

- Promoted the approved v0.5.0.18 prototype visual system from reference-only code into the real desktop shell.
- Replaced dummy prototype server tabs with the authoritative connection-profile collection.
- Connected ribbon lifecycle, backup, console and Doctor actions to the existing ViewModel/API/headless paths.
- Retained every restored functional page inside the glass workspace, with Home, World and System navigation matching the approved information architecture.
- Added custom title-bar behavior while preserving tray-aware close semantics.
- Added versioned logic/runtime/Linux gates and parity evidence for the corrected shell foundation.

## v0.5.1.1 — Home Navigation Refinement

- Creates a Home section containing Dashboard and Notifications.
- Removes Notifications from System; Settings and Activity & Audit remain there.
- Restyles the selected Home destination with the compact blue glass treatment from the approved prototype reference.
- Keeps the header notification bell routed to the persistent Notifications page.
- Preserves the v0.5.1.0 functional wiring, single status poll, secured remote/LAN boundary, card containment and Windows/Linux parity.

## v0.5.1.0 — Functional Visual Shell Integration

- Starts the post-restoration integration route from the clean v0.4.18.2 functional baseline.
- Uses the supplied compact MystTiq wordmark beside the Palworld server icon with the version directly beneath it.
- Keeps World navigation to Inspector, Players, Bases and Guilds; the transactional recovery center remains available inside Inspector.
- Keeps Settings, Activity & Audit and Notifications under System and adds direct header bell/gear routing.
- Adds a glass-highlight navigation hover while preserving global card clipping and centered metallic buttons.
- Preserves the headless-first API boundary, single aggregate poll, remote/LAN security, and Windows/Linux Avalonia parity.

## v0.4.18.2 — Closeout Gate Contract Correction

- Corrects inherited logic assertions that still expected v0.4.17.4 metadata and the former Workspace button label.
- Carries the complete v0.4.18.1 Workspace, Diagnostics, Settings and 287-event coverage implementation unchanged.

## v0.4.18.1 — Workspace + Diagnostics + Settings Closeout

- Corrects the initial v0.4.18.0 path-validator compile mismatch.
- Restores Workspace refresh, syntax validation, local browse/open and rollback-backed API save; local shell operations are disabled for remote profiles.
- Adds diagnostic report export and a local redacted ZIP support package while retaining network/firewall/listener routes.
- Retains connection profiles, LAN discovery, process-memory-only bearer tokens, TLS certificate pins and explicit local bootstrap.
- Classifies all 287 legacy GUI event rows with current mapping and evidence.
- Closes the planned restoration sequence and stops before v0.5.0.0.

## v0.4.9.1 — RCON Boundary Gate Correction

- Corrects the RCON ownership assertion so descriptive GUI text containing the word “socket” is not mistaken for socket implementation code.
- Continues to require all `TcpClient`/socket transport construction to remain in the shared headless Core service.
- Preserves the v0.4.9.0 Console + RCON implementation, single status poll, global card containment, remote/LAN security, and Windows/Linux Avalonia parity unchanged.

## v0.4.9.0 — Console + RCON Parity & Global Card Containment

- Accepts v0.4.8.0 as the source baseline and corrects its stale Testing UX and Palworld Configuration logic assertions.
- Restores legacy console severity/category/search filters, Hide routine REST, Refresh, Pause/Resume, Clear View and local Export.
- Adds Core-owned Source RCON support with headless `/api/v1/rcon/status`, `/api/v1/rcon/doctor`, and `/api/v1/rcon/command` routes; AdminPassword never crosses the API boundary.
- Adds legacy RCON Doctor/Connect/Disconnect/preset/send/history UI while clearly labelling RCON as deprecated/compatibility functionality.
- Adds a global card-containment style: card/status-card clipping, card text wrapping, and nested layout clipping to prevent content overflow.
- Adds v0.4.9.0 logic/runtime coverage for console filters, RCON wiring/security, global containment, version consistency, and prior regression contracts.

## v0.4.8.0 — Configuration Parity

- Promotes v0.4.7.1 as the accepted source baseline and fixes its carried-forward version/documentation metadata mismatches.
- Restores Simple/Advanced Palworld configuration views, search/category filtering, historical QoL presets, dirty-state and validation.
- Adds cross-platform local JSON import/export through Avalonia StorageProvider; imports remain local until Save Changes routes through the management API.
- Preserves unknown/custom OptionSettings and rollback-backed server-side saves.
- Adds v0.4.8.0 logic coverage for configuration parity, file-boundary safety, presets, filtering, validation, and version consistency.

## v0.4.8.0 — Dashboard + Server Setup Parity Closeout

- Correct the Testing UX logic assertion so README validation targets `Test-v0.4.8.0-Logic.ps1` rather than the prior v0.4.6.8 harness.
- Promote v0.4.6.8 as the official baseline after its complete gate passed.
- Restore legacy-style Dashboard OPEN/BACKUP/DOCTOR card workflows and the operational health strip without adding a second poller.
- Make RCON/Cleanup capability gaps visible as BACKEND REQUIRED instead of fake controls.
- Restore Server Setup banner/environment-health/checklist/operations/monitor hierarchy.
- Extend `/api/v1/server/environment` rows with `actionSupported` and `unavailableReason`; unsupported mutations are disabled and guarded in the ViewModel.
- Add v0.4.8.0 logic/runtime/Linux acceptance coverage for the reconstruction contract.

## v0.4.6.8 — Dashboard Meter Containment Fix

- Constrained the compact CPU/Memory utilization meters to the dashboard card using clipping and bounded meter layout.
- Clipped the custom Resource History chart to its own drawing bounds and inset plotted strokes so antialiasing cannot paint outside the card.
- Added logic-test coverage for dashboard meter/chart containment.

## v0.4.6.7 — Tray Gate Contract Fix & GUI Restoration Roadmap

- Corrected the stale Tray Lifecycle logic assertion to recognize the implemented Safe Exit, Force Exit and Exit GUI Only tray controls.
- Packaged the supplied GUI Reconstruction Guide, 287-event v0.2.16.4 event inventory and v0.2.16.4-to-current functional crosswalk as reconstruction references.
- Added `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md`, defining exact page/section restoration builds, current ViewModel/API mappings, BACKEND REQUIRED boundaries and per-version logic tests.
- Made the dangerous world/save mutation contract explicit: Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI.
- Preserved the v0.4.6.6 runtime functionality; this build is a release-gate/reference correction, not a backend architecture rewrite.

## v0.4.6.6 — Resource History Graph & Runtime Metrics Parity

- Restored the v0.2.16.4-style Resource History card with a live CPU/RAM line graph, 1 Hour / 6 Hours / 24 Hours / 7 Days / 30 Days range selector, average/peak summaries, sample count and live indicator.
- Added persisted headless historical telemetry under the manager runtime root with 30-day retention and throttled recording from the existing aggregate status poll; no second periodic poller was introduced.
- Added `/api/v1/history` for on-demand range changes while current live samples continue to arrive through the single `/api/v1/status/poll` path.
- Changed runtime metrics sampling to aggregate all managed PalServer processes so CPU, memory and thread totals do not go idle when the bootstrap/wrapper PID differs from the Shipping process.
- Restored compact CPU and RAM meters in the top dashboard summary and kept the full dual-series history graph in Resource History.
- Corrected the v0.4.6.4 Page Parity assertion so the intentionally restored `SERVER ENVIRONMENT` heading satisfies Server Setup parity.
- Added Windows runtime-smoke coverage for the historical-metrics endpoint and logic coverage for graph rendering, range selection, persistence and multi-process metrics aggregation.

## v0.4.6.3 — Deployment Bootstrap & Dashboard Telemetry Parity

- Added safe latest-FullSource deployment from Downloads with clean/stop, staged validation, target replacement, script unblocking and post-install validation.
- Replaced dashboard CPU bar with a live history graph sourced from the existing aggregate status poll.
- Added active-world nickname + full copyable World ID and restored authoritative World Pulse day/time, save-age and backup-age presentation.
- Combined redirected PalServer process output with Pal.log and restored green in-app console presentation.
- Restored dashboard server name/description, removed duplicate Dashboard heading, and repaired the persistent sidebar status layout.
- Expanded aggregate `/api/v1/status/poll` with dashboard world, backup and Palworld settings support data without adding another client polling loop.


## v0.4.6.2 — PalServer Window Suppression & Live Dashboard Parity

- Reapplies the proven v0.2 Windows post-launch window policy with Win32 `ShowWindow(SW_HIDE)` during PalServer startup so wrapper/child consoles that allocate after `Process.Start` are hidden while stdout/stderr remain redirected into MystTiq.
- Rebuilds the Avalonia Dashboard toward v0.2.16.4 information density with live health, world pulse, session/player state, resource history, live activity, console tail, online players and compact operational summaries.
- Preserves the single aggregate status polling path; dashboard animation/data surfaces consume the same coherent sample rather than creating extra pollers.
- Adds regression coverage for post-launch child-window hiding and dense live-dashboard parity.

## v0.4.6.1 — Hidden PalServer Console & In-App Output Redirection

- Promotes the successful v0.4.6.0 management-session/server-start behavior into the next tracked fix build.
- Windows PalServer launch no longer uses Unreal `-log`, which explicitly opens a separate log window.
- Windows default launch now uses `-stdout -FullStdOutLogOutput -logformat=text` with `UseShellExecute=false`, redirected stdout/stderr, and `CreateNoWindow=true`.
- MystTiq prioritizes `MystTiq-PalServer-Console.log` as the Live Console source, falling back to `Pal.log` only when redirected capture is unavailable.
- Adds regression coverage for hidden-window launch and Live Console redirection.

- Promoted v0.4.5.1 as the official baseline after its compile/logic/runtime gate passed.
- Versioned `/healthz` with API contract, backend version, platform, authentication and TLS metadata.
- Changed desktop connection establishment to use health verification followed by the single aggregate status poll instead of separate status/service calls.
- Added `--desktop-sidecar`, which forces a GUI-owned sidecar to loopback-only unauthenticated/non-TLS mode without weakening remote/LAN security rules.
- Added compatibility-aware loopback bootstrap: an occupied or incompatible configured endpoint causes the packaged sidecar to launch on a private free loopback port.
- Improved lifecycle connection failure text to report the exact endpoint and retained backend detail.
- Restored the established v0.2 default PalServer launch arguments and explicit text logging flags when no custom headless configuration exists.
- Carried the v0.2.16.4 GUI parity audit forward as `docs/GUI_PARITY_v0.4.6.1.md`.

# Changelog

## v0.4.11.0 — Guilds + Bases Parity

- Restored read-only guild and base directory/detail workflows with search/status filters, CSV export, ID copy, and guild-leader player navigation.
- Carried authoritative decoded base IDs through the headless explorer contract without adding GUI filesystem access.
- Kept all guild/base repairs and recovery visibly unavailable until the complete transactional safety backend exists.

## v0.4.10.0 — Players Parity

- Unified live REST and saved-player evidence into a server-owned stable-ID player directory with search, view/admin filters, details, save discovery, and CSV export.
- Added headless-persisted player notes and warnings with bounded input, atomic writes, and privacy-conscious audit entries.
- Made Kick/Ban availability follow online state and kept unsupported player operations visibly disabled.
- Centered global button content, added the metallic gradient interaction treatment, and guarded build relaunch by exact artifact-root process ownership.
- Preserved single aggregate periodic polling, remote/LAN security, card containment, and Windows/Linux Avalonia/headless parity.

## v0.4.5.1 — runtime parity continuation
- Fixed Avalonia desktop compilation for the Palworld configuration list by replacing unsupported `ListBox.HorizontalContentAlignment` with supported `ListBoxItem`/template stretching; added regression coverage.

- Restored a full active Palworld `OptionSettings` configuration editor in the Avalonia Configuration page through a new headless `/api/v1/palworld/config` contract. Saves create timestamped rollback copies.
- Server startup readiness now follows the configured `PublicPort` from `PalWorldSettings.ini` instead of assuming UDP 8211.
- Removed generic/self-evident tooltips while preserving explanatory, safety, capability, path and destructive-action help.
- Added `docs/GUI_PARITY_v0.4.5.1.md`, a page-by-page comparison against the v0.2.16.4 WPF GUI; partial and not-yet-migrated functions are explicitly tracked instead of being represented as complete.

## v0.4.5.1 — Tray Lifecycle, Page Parity & Server Start Reliability (Release Candidate)

### Runtime acceptance continuation
- Fixed Avalonia AXAML property-element syntax introduced by the tooltip pass; tooltips remain on owning/interactive controls and templates/context-menu property elements no longer carry invalid attributes.
- Added an Avalonia XAML safety regression check to block the malformed property-element attribute pattern from returning.
- Added hover information tooltips across navigation, buttons and selection controls.
- Added right-click live-player administration: native Palworld REST Kick/Ban plus capability-gated Whisper/Promote/Give Item entries.
- Restored persistent MystTiq activity/audit logging and a dedicated Activity & Audit view.
- Restored Windows PalServer stdout/stderr capture to `MystTiq-PalServer-Console.log` when MystTiq launches the server.
- Restricted world discovery to canonical SaveGames directories so backup copies of `Level.sav` are not detected as live worlds.
- World Inspector now preserves the full World ID and supplies a compact nickname for readability.
- Corrected stale v0.4.5.0 version assertions/manifest/banner references and archived-test validation noise.


- Added an Avalonia system tray using the established MystTiq icon and NativeMenu. Closing the main window hides it to the tray; it no longer leaves the local sidecar running with no user-facing control surface.
- Added tray actions to show the GUI and route Start/Restart/Stop through the existing ViewModel/API lifecycle commands.
- Added explicit `Stop Local Management Backend & Exit` and `Exit GUI (keep backend running)` choices. The stop action is guarded to the exact sidecar PID/executable started by the current GUI session and never targets PalServer or a separately installed MystTiq service.
- Decoupled management-API connectivity from the human-readable connection/lifecycle status so a failed Start operation does not disable all future lifecycle retries.
- Start Server now attempts local backend recovery when needed, verifies the server distribution/executable before mutation, and surfaces a visible lifecycle result instead of failing silently.
- Split previously shared generic page presentation into page-specific Server Setup, Update Center, Base Manager, Guild Administration, Live Console, Activity & Audit, MOD Dashboard, MOD Library, and UE4SS Runtime views while retaining existing backend data contracts.
- Hardened the Windows runtime smoke test with an isolated missing-server root and a safe `/api/v1/server/start` request that must return HTTP 424; it can never launch the user's real PalServer.
- Added v0.4.5.1 logic/runtime/Linux acceptance coverage for tray lifetime, sidecar ownership, lifecycle retryability, start preflight, visible lifecycle results, and page-specific presentation.

## v0.4.5.0 — GUI Branding & Navigation Polish (Release Candidate)

- Promoted v0.4.4.3 as the official baseline after successful build, logic and runtime acceptance.
- Replaced the temporary Avalonia sidebar `M` placeholder with the established MystTiq Palworld Server Manager logo asset.
- Applied the established MystTiq icon to the Avalonia window/application packaging.
- Added a modest standardized indent to expanded navigation child items while preserving the shared 44-pixel navigation row height.
- Added v0.4.5.0 logic/runtime/Linux acceptance coverage and branding/navigation regression checks.
- Preserved single aggregate status polling, local sidecar auto-connect, LAN/remote authentication/TLS, and Windows/Linux platform behavior.

## v0.4.4.3 — Local Sidecar Auto-Connect & Runtime Acceptance Fix (Promoted Baseline)

- Fixed the acceptance-blocking Windows architecture gap where `api-run` rejected Windows, leaving the Avalonia GUI able to discover PalServer files but unable to perform management operations.
- Added Windows Core lifecycle/session inspection and Windows management API composition.
- Added a self-contained headless sidecar to Windows/Linux Avalonia desktop publishes and local API bootstrap behavior when no persistent API is reachable.
- Standardized sidebar top-level and expanded child navigation rows to the same 44-pixel sizing.
- Added a Windows runtime-smoke gate for health, aggregate status, configuration and distribution endpoints.
- Added platform-aware Windows/Linux headless configuration defaults and path validation.
- Local loopback sidecars now auto-connect without remote credentials when `/healthz` explicitly reports authentication disabled; LAN/remote services retain bearer/TLS requirements.
- Corrected the desktop-sidecar packaging assertion to validate the actual publish contract instead of a brittle regex.
- Corrected stale v0.4.4.2 expectations accidentally carried into the v0.4.4.3 logic harness for version, README, roadmap and test-command checks.
- Hardened `Build.ps1 Clean` so auto-launched development GUI/sidecar processes under `artifacts` are stopped before deletion; Clean now fails loudly if generated artifacts remain instead of reporting a false success.
- Promoted as the official baseline after the complete release gate and runtime acceptance passed.


## v0.4.4.1 — Server / Configuration / Console / Workspace Integration (Release Candidate)


- Fixed release packaging so `scripts/Test-v0.4.4.1-Logic.ps1` is present in both Changed Files and Full Source packages.
- Added bounded cross-platform LAN discovery for MystTiq `/healthz` endpoints instead of assuming only `127.0.0.1`.
- Preserved secure remote management: discovery may identify a service, while authenticated API operations still use normal TLS validation/certificate pinning and bearer-token rules.
- Standardized Avalonia sidebar navigation rows at 36 px with compact indentation/font sizing so expanded items fit consistently across Windows and Linux.
- Added logic contracts for dynamic version wiring, package/test presence, LAN discovery behavior/security, and GUI discovery composition.
- Standardized the documented full test sequence: unblock scripts, Clean, Validate, then the current version logic test with `-RunBuild -ExportJson`.

- Added one aggregate `/api/v1/status/poll` endpoint so periodic GUI status sampling does not fan out across multiple HTTP requests.
- Added a cross-platform bottom status bar with last-sample time and an indeterminate busy animation for active work.
- Promoted Workspace from placeholder navigation to a managed, API-backed view of server, SteamCMD, backup, and runtime paths.
- Preserved lifecycle ownership in the headless/Core path; Avalonia remains an API consumer on Windows and Linux.
- Added `Test-v0.4.4.1-Logic.ps1` covering View → ViewModel → API → endpoint → Core/platform wiring, polling behavior, workspace behavior, release-gate integration, and platform preservation.
- Updated the release workflow so logic tests and Windows/Linux headless + Avalonia builds execute before packaging/checksums.

- Development moved to v0.4.4.1 after promotion of v0.4.3.1.
- Target architecture remains headless-first: View -> ViewModel -> API/service -> endpoint -> Core -> platform implementation.
- GUI close/background behavior must remain independent of explicit server/service shutdown.
- Windows and Linux desktop behavior must share Avalonia/Core contracts wherever platform-specific implementations are not required.
- Versioning now follows MAJOR.MINOR.REVISION.FIX; new revisions start at fix 0.

## v0.4.3.1 — Promoted Baseline / Avalonia StringFormat Parser Hotfix

- Promoted v0.4.3.1 as the official baseline after the complete release gate passed.
- Normalized the former `v0.4.0.3 FIX1` label to the new four-part version convention.
- The final component is now the fix number; separate `FIX1` suffixes are no longer used for new releases.
- Preserved the Dashboard/local backend integration and Avalonia StringFormat correction.

## v0.4.0.2 FIX2 — Console Composition & Warning-Free Build Hotfix

- Corrected Console composition verification to follow its real Players/Logs/Metrics API wiring.
- Fixed CS8601 in diagnostics restart handling.
- Avalonia desktop compiler warnings now block promotion.
- Product version remains v0.4.0.2.


## v0.4.0.2 FIX1 — Logic Harness & Release Packaging Hotfix

- Fixed the v0.4.0.2 PowerShell logic-harness parser error.
- Added required changed-files apply instructions and packaging preflight checks.
- Product/runtime behavior remains v0.4.0.2.


## v0.4.0.2 — Avalonia Navigation & Theme Foundation

- Rebuilt the Avalonia left navigation using the original MystTiq SERVER/WORLD/MODS/TOOLS/SYSTEM hierarchy.
- Added persistent sidebar service/server status and version identity.
- Replaced the purple prototype accent with centralized dark navy/blue theme resources.
- Added shared primary/success/warning/danger/navigation/card/status styles.
- Routed navigation destinations to existing backend-backed screens where available and explicit placeholders where backend integration is deferred.
- Added current-version Linux acceptance/production-readiness packaging scripts.
- Integrated version-specific logic/regression tests into the root Build.ps1 release gate.
- Re-sequenced v0.4.0.3–v0.4.0.9 for GUI functional parity before deferred save/freeze and advanced MOD diagnostic work.

## v0.4.0.1 — Network Diagnostics & Connectivity Recovery

- Added independent Runtime Health and Network Health.
- Added effective Palworld UDP game-port resolution with explicit `-port=` support and safe 8211 fallback.
- Added PID-aware UDP/TCP endpoint inspection and wrong-process/wrong-port detection.
- Treats `0.0.0.0` as a healthy all-IPv4 binding and resolves a human-readable LAN endpoint.
- Added Windows Firewall rule inspection plus idempotent MystTiq-owned rule repair.
- Added startup grace for delayed PalServer socket binding.
- Added optional RCON and REST checks when enabled in PalWorldSettings.ini.
- Added controlled diagnostic restart with post-restart network verification.
- Added management API endpoints and shared Avalonia Diagnostics UI.
- Added redacted support report/copy workflow and structured diagnostic start/end evidence.
- Captured projected v0.4.0.2–v0.4.0.5 save/freeze, auto-save/save-health, incident-bundle and MOD isolation/functional-test work.


## v0.4.0.0 — Windows Persistent Service Foundation

- Added Windows SCM service manager contracts and implementation in shared core.
- Added automatic-start/recovery configuration for the MystTiq Windows Service.
- Added win-x64 headless-host publish build action.
- Preserved Linux systemd behavior and existing WPF runtime while Windows service lifecycle integration proceeds.


## v0.3.1.9 FIX1 — Release Metadata & Deployment Script Cleanup

- Added the required v0.3.1.9 Changed Files apply document.
- Synchronized Linux desktop deployment helper version references to v0.3.1.9.
- No runtime behavior changed.


## v0.3.1.9 — MOD & UE4SS Management

- Added shared MOD/UE4SS API and Avalonia management page.
- Added UE4SS active-root and runtime-evidence parity.
- Added safe stopped-server PAK and mods.txt state changes.
- Preserved neutral Disabled / Active-Unverified health semantics.
- Removed superseded v0.3.1.8 harness warning source.


## v0.3.1.8 — Player & Guild Explorer

- Added validated active-world player-save identity discovery.
- Added authenticated Player & Guild Explorer API.
- Added authoritative decoded GroupSaveDataMap guild semantics.
- Added optional live REST enrichment for online player evidence.
- Added guild leadership/member/base-reference and orphan-review evidence.
- Added shared Avalonia Players & Guilds page.
- Preserved read-only behavior and explicit semantic-unavailable state.


## v0.3.1.7 FIX1 — Validation Warning Cleanup

- Removed the prior-patch literal from the active v0.3.1.7 logic harness.
- Replaced it with a positive assertion that `DoctorReport` receives the assembly-derived `reportVersion`.
- No runtime behavior changed.


## v0.3.1.7 — World Explorer Foundation

- Added read-only authenticated World Explorer API.
- Added server-authoritative world discovery from SaveRoot/Level.sav evidence.
- Added bounded active-world file/player-save metadata inventory.
- Added shared Avalonia World Explorer page.
- Made Doctor version assembly-derived.
- Changed inapplicable secured-listener lifecycle acceptance from Warning to Skip.
- Added skipped-count evidence to Linux acceptance.


## v0.3.1.6 — Server Setup & Update Workflows

- Began v0.3.1.6+ feature-parity expansion.
- Added service-owned SteamCMD/Palworld server distribution status and plan APIs.
- Added API-backed Palworld Dedicated Server update/validation.
- Added stopped-server, serialization, cancellation and post-update verification safeguards.
- Added shared Avalonia Setup & Update page.
- Kept MystTiq application updates explicitly separate from Palworld server updates.


## v0.3.1.5 FIX1 — Desktop Version Metadata Cleanup

- Synchronized PalworldManager manifest release metadata to v0.3.1.5.
- Synchronized Avalonia desktop project metadata to v0.3.1.5.
- Added version-metadata regression checks.
- No runtime behavior changed.


## v0.3.1.5 — Production Doctor & Diagnostics GUI

- Added authenticated `/api/v1/doctor` endpoint and shared production-health evidence model.
- Added functional Avalonia Doctor page with PASS/WARNING/FAIL summary, evidence, recommendations, timestamp, and report export.
- Added Linux acceptance coverage for the Doctor endpoint.


## v0.3.1.4 — Backup & Configuration

- Added authenticated backup inventory/create/delete/restore API.
- Added managed filename/path containment checks and verified `.partial` backup commit.
- Added safe restore requiring PalServer stopped, pre-restore safety backup, and staging/rollback.
- Added restricted headless configuration GET/PUT API.
- Added validation + timestamped rollback copy before configuration writes.
- Preserved authentication/TLS security structures outside the editable DTO.
- Added functional Avalonia Backups page and headless configuration editor.
- Added v0.3.1.4 Linux acceptance coverage for backup/config read contracts.


## v0.3.1.3 FIX6 — Harness Assertion Cleanup

- Replaced one brittle exact-line deployment assertion with behavior-oriented checks.
- Preserved parser-safe version resolution and dynamic archive-selection regression coverage.
- No runtime code changed.


## v0.3.1.3 FIX5 — PowerShell Deploy Parameter Syntax Hotfix

- Replaced invalid command invocation in the `Version` parameter default.
- Current project version is now resolved after parameter binding and project-root resolution.
- Preserved optional explicit `-Version` override.
- Added parser-pattern regression checks.
- No runtime behavior changed.


## v0.3.1.3 FIX4 — Dynamic Deployment Version & Validation Cleanup

- Removed hard-coded v0.3.0.7 deployment version.
- Headless deployment now derives the current project version dynamically.
- Headless archive selection is version-driven.
- Linux acceptance/production gate paths remain dynamically versioned.
- No runtime behavior changed.


## v0.3.1.3 FIX3 — Linux Gate Version-Path Validation Cleanup

- Eliminated six false stale-version warnings caused by literal versioned Linux gate filenames in the logic harness.
- Current Linux acceptance/production script paths are now derived from `Get-ProjectVersion.ps1`.
- Preserved all Linux monitoring gate checks.
- No runtime behavior changed.


## v0.3.1.3 FIX2 — Linux Acceptance & Production Gate Restoration

- Added release-version-matched v0.3.1.3 Linux acceptance and production-readiness runners.
- Added monitoring endpoint/payload checks to Linux acceptance.
- Added explicit evidence that AdminPassword is absent from the player API response.
- Removed legacy v0.3.0.7 Linux gate scripts from the active full-source baseline.
- Updated cleanup automation for ChangedFiles overlays.


## v0.3.1.3 FIX1 — Headless Monitoring HttpVersion Compile Hotfix

- Added the missing `System.Net` namespace required by `HttpVersion.Version11`.
- Preserved explicit HTTP/1.1 Palworld REST requests.
- Added compile-contract regression checks.
- No runtime behavior changed.


## v0.3.1.3 — Logs, Players & Monitoring

- Added authenticated `/api/v1/players`, `/api/v1/logs/tail`, and `/api/v1/metrics` endpoints.
- Added server-side Palworld REST player polling over loopback only.
- Kept Palworld AdminPassword server-side; it is never returned to Avalonia.
- Added bounded PalServer log-tail reads.
- Added PalServer CPU, working-set RAM and thread metrics.
- Added shared Players and Monitoring pages.
- Added Dashboard player-count/CPU/RAM summaries and recent metric history.
- Preserved v0.3.1.2 lifecycle behavior and v0.3.1.1 connection-profile security.


## v0.3.1.2 FIX1 — Version Consistency Cleanup

- Removed obsolete v0.3.1.1 cleanup script from active source.
- Removed literal stale-version text from the current cleanup helper.
- Updated Avalonia desktop project metadata to v0.3.1.2.
- Added regression checks for version-consistency hygiene.
- No runtime behavior changed.


## v0.3.1.2 — Dashboard & Lifecycle

- Added API-backed Start / Stop / Restart to the shared Avalonia desktop.
- Added live PalServer readiness, PID and UDP listener evidence.
- Added MystTiq system-service state.
- Added last-observed, last-transition and uptime presentation.
- Added optional five-second auto refresh.
- Lifecycle mutations reacquire authoritative status/service evidence.
- Desktop remains an API client and never directly owns PalServer.


## v0.3.1.1 FIX1 — Deployment Version & Warning Cleanup

- Removed stale v0.3.1.0 references from the Linux desktop deployment helper.
- Made Linux desktop archive naming version-driven.
- Removed the unused generic `CanExecuteChanged` backing-field compiler warning.
- Added regression checks for both cleanup items.
- No runtime behavior changed.


## v0.3.1.1 — Shared Shell, Navigation & Connection Foundation

- Added working Avalonia navigation across Dashboard, Server, Players, Backups, Mods, Doctor and Settings.
- Added persistent local/remote connection profiles.
- Bearer tokens remain process-memory-only and are never saved with profiles.
- Added optional SHA-256 TLS certificate pinning while preserving normal OS trust validation by default.
- Added automated Linux Avalonia desktop build/deploy/hash/smoke-launch helper.
- GUI remains an API client and does not own PalServer lifetime.


## v0.3.1.0 FIX6 — Actual RunBuild Block Locator Hotfix

- Replaced first-string `IndexOf()` RunBuild detection with a line-level regex locator.
- The harness now selects the final real `if($RunBuild){` block instead of its own string literal.
- Independently validated the extracted build block during package generation.
- No application/runtime behavior changed.


## v0.3.1.0 FIX5 — RunBuild Boundary Marker Hotfix

- Corrected the logic harness RunBuild end marker from nonexistent `$report=` to the actual `$passed=` summary boundary.
- Added regression coverage for the real RunBuild boundary marker.
- No application/runtime behavior changed.


## v0.3.1.0 FIX4 — RunBuild Block Boundary Hotfix

- Fixed the final false-negative build-gate regression check.
- RunBuild source inspection now stops at the report-generation boundary instead of scanning the remainder of the harness.
- Prevents the regression assertion from detecting its own `$LASTEXITCODE` text.
- No application/runtime behavior changed.


## v0.3.1.0 FIX3 — Harness Self-Reference Regression Hotfix

- Fixed two false-negative FIX2 logic checks caused by self-referential source scanning and PowerShell string interpolation.
- Build-gate regression checks now inspect only the actual RunBuild block.
- Validate, Windows desktop and Linux desktop non-throwing-completion semantics are all checked structurally.
- No application/runtime behavior changed.


## v0.3.1.0 FIX2 — PowerShell Build-Gate Exit Semantics Hotfix

- Fixed false harness failure caused by checking stale `$LASTEXITCODE` after successful PowerShell build-script execution.
- PowerShell build actions now PASS on non-throwing completion and FAIL on exceptions.
- Added regression coverage preventing native exit-code semantics from being reused for PowerShell build actions.
- No runtime application behavior changed.


## v0.3.1.0 FIX1 — Avalonia Font Dependency & Desktop Compile-Gate Hotfix

- Added the missing `Avalonia.Fonts.Inter` dependency required by `.WithInterFont()`.
- Updated the public docs index to the v0.3.1.0 desktop foundation candidate.
- Strengthened the v0.3.1.0 `-RunBuild` harness to compile/publish Windows and Linux Avalonia targets.
- Added regression checks tying the Inter bootstrap to its package dependency.
- No accepted headless/WPF/server runtime behavior changed.


## v0.3.1.0 — Avalonia Desktop Foundation

- Added the first shared Windows/Linux Avalonia desktop project.
- Added MVVM shell, MystTiq theme foundation and API client boundary.
- Added local connection profile and live `/api/v1/status` connection.
- Added Windows/Linux desktop publish build actions.
- Preserved Windows WPF during cross-platform parity development.


## v0.3.0.7 FIX4 — Headless Help, Linux Documentation & GUI Roadmap Completion

- Added `production-doctor` to built-in headless help.
- Added complete Linux command/path/security documentation.
- Added Linux production workflow to the main README.
- Selected Avalonia for v0.3.1.x shared Windows/Linux GUI.
- Defined v0.3.1.0 as Avalonia Desktop Foundation.
- No accepted Linux runtime behavior changed.


## v0.3.0.7 FIX3 — Production Readiness Result Accounting Hotfix

- Fixed Bash post-increment exit-status behavior causing PASS checks to also execute FAIL branches.
- `record` now uses assignment-based counter increments and explicitly returns success.
- Executable and disk-reserve checks now use explicit `if / elif / else` control flow.
- Added regression coverage for production-readiness result accounting.
- The underlying Production Doctor remains unchanged and had already passed 9/9 checks.


## v0.3.0.7 FIX2 — Extended Production-Readiness Integration Hotfix

- Fixed extended Linux acceptance not invoking the packaged v0.3.0.7 production-readiness runner.
- Extended acceptance now passes the active executable and config paths into the production-readiness test.
- Production-readiness output is captured in the timestamped Linux acceptance report.
- A readiness failure now increments the Linux acceptance failure count and blocks promotion.
- No runtime/API/lifecycle/systemd/TLS/PalServer/Windows WPF behavior changed.


## v0.3.0.7 FIX1 — Production Doctor Compile & Release-Gate Hotfix

- Fixed Production Doctor references to the existing `IServerPathProfile` contract.
- Routed PalServer readiness through the established `LinuxServerLifecycleService`.
- Fixed the v0.3.0.7 logic harness architecture path.
- Removed active stale v0.3.0.6 HeadlessHost wording.
- Prevented the cleanup helper from creating stale-version warnings.
- Corrected apply order: Unblock PowerShell scripts before running cleanup.


## v0.3.0.7 — Linux Integration & Production Readiness

- Added production-doctor with evidence/recommendation output.
- Added first-run and safe-upgrade Linux automation.
- Added one-command production-readiness reporting.
- Integrated production readiness into extended Linux acceptance.
- Reserved v0.3.0.8+ for stabilization only.


## v0.3.0.6 FIX5 — Acceptance Harness Consistency Hotfix

- Fixed two false-negative logic-harness checks after successful real-world remote API acceptance.
- Enrollment-version validation now matches the stable v0.3.0.6 base contract instead of exact decorated FIX text.
- Token-persistence validation now checks stable semantic markers rather than exact spacing-sensitive output.
- Synchronized Linux enrollment and Windows remote-acceptance display labels to FIX5.
- No runtime/API/TLS/auth/systemd/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX4 — Running-Binary Service Install Hotfix

- Fixed Linux `service-install` failing with `Text file busy` while `/opt/mysttiq/bin/mysttiq-server` was running.
- Replaced in-place executable overwrite with same-directory staged copy plus atomic rename.
- Existing systemd restart behavior now launches the replaced binary while the old process can finish on its previous inode.
- Added regression coverage for staged executable replacement.
- No API, TLS, authentication, configuration schema, PalServer lifecycle policy, or Windows WPF behavior changed.


## v0.3.0.6 FIX3 — Protected Secrets Directory Ownership Hotfix

- Fixed `/etc/mysttiq/secrets` being created as root-only while MystTiq secrets were owned by the non-root service user.
- Secrets directory is now owned by the selected service account with mode 0700.
- Enrollment explicitly verifies secrets-directory owner/mode.
- Token/certificate existence checks now use privilege-safe `sudo test -s` checks.
- Preserved per-file mode 0600 and pre-commit rollback behavior.
- No runtime/API/security-policy/lifecycle/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX2 — Linux Remote-Tool Packaging Hotfix

- Fixed Linux headless archives omitting the v0.3.0.6 remote-enrollment and rollback scripts.
- `Build-LinuxHeadless.ps1` now packages the acceptance runner, remote enrollment script, and remote disable script.
- Missing required Linux-side scripts now fail the package build instead of producing an incomplete archive.
- Added regression coverage for all required Linux operational scripts.
- No runtime, API, security, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 FIX1 — Remote Enrollment Reliability Hotfix

- Fixed remote enrollment leaving MystTiq on the previous loopback/schema-v1 configuration.
- Reworked Linux enrollment into a staged fail-fast workflow with prerequisite, file, ownership, configuration, service, listener, HTTPS and authentication verification.
- Added automatic pre-enrollment configuration backup and pre-commit rollback.
- Enrollment now verifies token/PFX/password creation instead of assuming commands succeeded.
- Enrollment now reads the effective config back and requires the requested bind plus auth/TLS before restarting.
- Enrollment now verifies the exact LAN listener before reporting success.
- Windows remote acceptance now validates effective config, token and listener prerequisites before attempting HTTPS.
- Missing token/listener/connectivity now produces explicit `[FAIL]` output instead of null-reference PowerShell exceptions.
- No core API security policy, configuration schema, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 — Secure Remote API Enrollment & TLS Provisioning

- Promoted v0.3.0.5 as the official Linux/headless baseline.
- Added `HeadlessCertificateService` for self-signed TLS server certificate provisioning.
- Generated certificates use RSA 3072, SHA-256, server-auth EKU, selected IP SAN, localhost SAN, optional DNS SAN, and a maximum 825-day validity.
- PFX passwords are generated separately using the existing protected-secret service.
- Added `HeadlessRemoteApiEnrollmentService` for typed remote enable/disable configuration updates.
- Added `api-tls-create`, `api-remote-enable`, and `api-remote-disable`.
- Remote enable requires an explicit non-loopback literal IP and produces an auth+TLS configuration; remote disable returns to loopback defaults.
- Added `scripts/Configure-MystTiqRemoteApi.sh` for one-command Linux enrollment with confirmation, one sudo authorization, secret/certificate provisioning, service-user ownership hardening, systemd restart and HTTPS/auth validation.
- Added `scripts/Disable-MystTiqRemoteApi.sh` for one-command return to local-only management.
- Added `scripts/Test-MystTiqRemoteApi.ps1` for Windows-over-LAN HTTPS/401/bearer-auth acceptance using the dedicated SSH trust channel.
- Linux enrollment intentionally does not alter firewall rules.
- Extended the Linux acceptance runner with temporary TLS certificate creation and explicit secured remote-config/rollback tests.
- Windows WPF behavior remains unchanged.


## v0.3.0.5 — Passwordless Linux Deployment & SSH Trust Foundation

- Promoted v0.3.0.4 FIX1 as the official Linux/headless baseline.
- Added `scripts/Initialize-MystTiqLinuxSSH.ps1` for one-time dedicated Ed25519 deployment-key setup.
- The SSH bootstrap installs only the public key into `~/.ssh/authorized_keys`; the private key remains on Windows.
- Added passwordless-key verification using OpenSSH batch/public-key-only authentication.
- Reworked `scripts/Deploy-Test-MystTiqLinux.ps1` to prefer the dedicated MystTiq SSH key automatically.
- Deployment now uses the same dedicated identity for SSH and SCP.
- Interactive password deployment is disabled by default and available only through explicit `-AllowPasswordFallback`.
- Removed normal dependence on Posh-SSH/password caching from the deployment workflow.
- Preserved SHA-256 transfer validation, versioned extraction, and automated Linux acceptance invocation.
- Carried the version-matched Linux acceptance runner forward as `scripts/Test-v0.3.0.5-LinuxAcceptance.sh`.
- No management API, configuration schema, TLS/authentication, PalServer lifecycle, systemd, or Windows WPF behavior changed.


## v0.3.0.4 FIX1 — HeadlessHost Compile & Automation Harness Hotfix

- Fixed CS1929 by invoking `WaitForShutdownAsync(CancellationToken)` through the `IHost` generic-host interface.
- Corrected the deployment harness literal for the actual `& .\Build.ps1 LinuxHeadless` invocation.
- Prevented the release validator from treating the versioned Linux acceptance-script filename as a stale-version suffix.
- Added regression coverage for the `IHost` shutdown bridge.
- No security model, configuration schema, API behavior, lifecycle policy, Linux acceptance behavior, or Windows WPF behavior changed.


## v0.3.0.4 — Secure Management API & Automated Linux Acceptance Foundation

- Promoted v0.3.0.3 FIX1 as the official Linux/headless baseline.
- Advanced headless configuration to schema version 2 with API authentication and TLS settings.
- Added supported in-memory migration from schema v1 plus `config-migrate` persistence and automatic migration during `service-install`.
- Added protected secret-file service with cryptographically random bearer-token generation and Linux owner-only token permissions.
- Added `api-token-create` command.
- Added bearer-token authentication for `/api/v1/*` while keeping `/healthz` intentionally minimal and unauthenticated.
- Added fixed-time token comparison.
- Added TLS certificate/password-file support for Kestrel.
- Non-loopback API configuration now fails closed unless authentication and TLS are both enabled; the runtime API host independently enforces the same condition.
- Default configuration remains loopback-only and does not require authentication/TLS.
- Added version-matched `scripts/Test-v0.3.0.4-LinuxAcceptance.sh` with consolidated PASS/FAIL/WARN output plus timestamped raw evidence.
- Added `scripts/Deploy-Test-MystTiqLinux.ps1` to build, verify, copy, extract and invoke Linux acceptance in one workflow; default Linux test host is `192.168.1.248`.
- Linux publish archives now include their version-matched Linux acceptance runner.
- Added the detailed product roadmap through v0.8, including v0.4 character migration/Doctor/UI consolidation, v0.5 advanced administration, v0.6 multi-server, v0.7 themes/icon set and v0.8 adaptive efficiency.
- Windows WPF behavior remains unchanged.


## v0.3.0.3 FIX1 — Configuration Compile & systemd Argument-Quoting Hotfix

- Corrected Linux absolute-path validation to use `StartsWith("/", StringComparison.Ordinal)` instead of the invalid char/StringComparison overload.
- Added the missing `QuoteSystemdArgument(string value)` helper used by systemd `ExecStart` configuration-path generation.
- Added newline-injection rejection to the systemd argument-quoting helper.
- Strengthened the v0.3.0.3 harness to cover both compile contracts.
- No configuration schema, API, lifecycle, recovery, or Windows WPF behavior changed.


## v0.3.0.3 — Headless Configuration & Local Management API Foundation

- Promoted v0.3.0.2 FIX2 as the official Linux/headless baseline.
- Added schema-versioned persistent headless configuration with Linux defaults under `/etc/mysttiq/mysttiq.json`.
- Added configuration validation for paths, lifecycle/recovery values, API port, and API bind scope.
- v0.3.0.3 rejects non-loopback API binding by design.
- Added `config-show`, `config-validate`, and `config-write-default`.
- Added `config/mysttiq.linux.example.json`.
- Lifecycle CLI and service supervisor now consume configured timeouts, paths, recovery settings, and PalServer launch arguments.
- Added ASP.NET Core/Kestrel local management API support to the headless host.
- Added loopback-only health, lifecycle status, systemd status, configuration, and start/stop/restart endpoints.
- Lifecycle-changing API calls are serialized to prevent competing operations.
- `service-run` starts/stops the local API with the supervisor when API support is enabled.
- Added standalone `api-run` for local API testing without systemd.
- `service-install` creates a default configuration when one does not exist and preserves a custom `--config` path in systemd `ExecStart`.
- No remote/LAN API exposure, authentication secrets, or Windows WPF changes are introduced in this phase.
- Added Windows v0.4 backport candidates for shared configuration, local service IPC/API, and UI-to-background-service control.


## v0.3.0.2 FIX2 — systemd Start-Limit Section Hotfix

- Moved `StartLimitIntervalSec=300` and `StartLimitBurst=5` from `[Service]` to `[Unit]`.
- Preserved `Restart=on-failure` and `RestartSec=10` under `[Service]`.
- Added regression coverage for correct directive ordering.
- No lifecycle, recovery algorithm, PalServer launch, or Windows WPF behavior changed.


## v0.3.0.2 FIX1 — systemd Unit Raw-String Compile Hotfix

- Replaced the malformed raw-string `BuildUnit()` implementation with compile-safe string construction.
- Preserved the exact v0.3.0.2 systemd unit policy and lifecycle behavior.
- Strengthened the v0.3.0.2 harness to detect raw-string regression and verify generated unit sections.
- No Windows WPF behavior changed.


## v0.3.0.2 — Linux Service & Automatic Recovery Foundation

- Added systemd service models and `LinuxSystemdServiceManager`.
- Added `service-status`, `service-install`, `service-uninstall`, and internal `service-run` headless commands.
- Service installation copies the current self-contained headless host to `/opt/mysttiq/bin/mysttiq-server`, writes `/etc/systemd/system/mysttiq-palworld.service`, reloads systemd, and enables boot startup.
- Service start remains explicit unless `--start-now` is supplied.
- Added a long-running `LinuxHeadlessSupervisor` that starts/adopts PalServer, monitors lifecycle state, and performs bounded automatic crash recovery with configurable backoff/restart windows.
- Added SIGTERM/SIGINT handling so systemd shutdown asks MystTiq to perform the existing graceful PalServer shutdown policy.
- The service runs as the selected non-root service account, with `NoNewPrivileges=true`, `Restart=on-failure`, restart delay, and systemd start-rate limits.
- systemd journal output now captures MystTiq service/supervisor logs while PalServer console output remains under `/opt/mysttiq/runtime`.
- Promoted v0.3.0.1 FIX1 as the official Linux/headless baseline.
- Added v0.4 backport candidates for Windows Service supervision, automatic recovery throttling, background journal/event logging, and UI-independent startup recovery.


## v0.3.0.1 FIX1 — Linux Lifecycle Compile Contract Hotfix

- Added `FindProcessesByName(...)` to the shared `IServerSessionInspector` contract; the Linux implementation already provided the method and the lifecycle layer consumes it.
- Marked `LinuxServerLifecycleService` with `[SupportedOSPlatform("linux")]` so .NET platform analysis recognizes intentional Unix-only API use such as `File.SetUnixFileMode`.
- Strengthened the v0.3.0.1 harness to validate the shared process-discovery contract and Linux platform annotation.
- No lifecycle semantics or Windows behavior changed.


## v0.3.0.1 — Linux Server Lifecycle Control

- Added shared lifecycle phase, operation-result, persisted-state, and stable headless exit-code models.
- Added `LinuxServerLifecycleService` with `start`, `stop`, `restart`, and lifecycle-aware `status`.
- Linux start blocks duplicate PalServer instances, launches the server detached from the SSH terminal, observes the native `PalServer-Linux-Shipping` process, and verifies UDP 8211 before reporting ready.
- Detached PalServer stdout/stderr is written to `/opt/mysttiq/runtime/palserver-console.log`.
- Added persisted lifecycle state under `/opt/mysttiq/runtime/lifecycle-state.json` for transition/crash evidence across short-lived CLI invocations.
- Linux stop sends SIGTERM first and escalates to SIGKILL only after the graceful timeout; force escalation is surfaced in the operation result.
- Restart safely stops an active server before starting it; restart from stopped/crashed state proceeds as a start.
- Added Ctrl+C cancellation handling and configurable startup/stop timeouts to the headless CLI.
- Corrected Linux kernel reporting to read `/proc/sys/kernel/osrelease`; the validated host previously exposed distro text through `RuntimeInformation.OSDescription`.
- Preserved `probe` and informational `install-plan`, including the validated SteamCMD `+@sSteamCmdForcePlatformType linux` requirement.
- Windows WPF behavior remains unchanged; lifecycle/service backport opportunities are recorded for v0.4.

## v0.3.0.0 FIX1 — Regression Harness Reserved Variable Hotfix

- Corrected the v0.3.0.0 PowerShell harness to avoid the read-only built-in `$Host` variable by using `$headlessHostSource`.
- Runtime validation then passed 55/55 and the self-contained Linux host successfully detected Ubuntu, SteamCMD, `PalServer-Linux-Shipping`, and UDP 8211.


## v0.3.0.0 — Headless Core Foundation & Linux Platform Base

- Added `MystTiq.Core` as a new `net10.0` cross-platform project with no WPF dependency.
- Added `MystTiq.HeadlessHost` as a no-GUI command-line host for Linux/headless development.
- Added platform-neutral runtime configuration separate from the Windows WPF `AppSettings` model.
- Added Linux distribution detection through `/etc/os-release` plus kernel/architecture reporting.
- Added Linux server path profile using `/opt/mysttiq/palserver`, `/opt/mysttiq/steamcmd/steamcmd.sh`, LinuxServer config paths, logs, saves, and backup roots.
- Added Linux process/executable profile for `PalServer.sh` and `Pal/Binaries/Linux/PalServer-Linux-Shipping`.
- Added `LinuxServerDistributionPlatformService` with Valve Linux SteamCMD package handling and Palworld App `2394010`.
- Baked the validated `+@sSteamCmdForcePlatformType linux` SteamCMD override into Linux install/update argument generation after the reference environment returned `Missing configuration` without it.
- Added read-only Linux procfs session inspection for process trees, mapped files, and guarded TCP/UDP port observation.
- Added non-destructive headless commands: `probe`, `status`, and `install-plan`; v0.3.0.0 does not yet grant the headless host start/stop/install/update authority.
- Added a dedicated Linux self-contained publish workflow through `Build.ps1 LinuxHeadless`.
- Added Linux tested-environment documentation for Ubuntu Server 24.04.4 LTS x86_64, kernel `6.8.0-137-generic`, Valve Linux SteamCMD, and native PalServer validation.
- Added the formal SHARED / LINUX / WINDOWS-BACKPORT registry; Windows headless/service/minimized-resource improvements are reserved for v0.4 unless shared-core correctness requires earlier work.
- Preserved v0.2.16.4 as the frozen validated Windows baseline; no intentional WPF behavior changes are included in this phase.


## v0.2.16.4 — SteamCMD Distribution Abstraction & Final Windows Platform Audit

- Added `IServerDistributionPlatformService` as the platform boundary for SteamCMD package source, extraction, command arguments, process startup policy, and default Palworld install recovery.
- Added `WindowsServerDistributionPlatformService`, preserving the validated Windows SteamCMD behavior and Palworld Dedicated Server App ID 2394010.
- Added `ServerDistributionPlatformService.ForCurrentPlatform()` so higher-level installer/update services no longer construct the Windows implementation directly.
- Migrated `InstallerService` SteamCMD install/self-update and Palworld server install/retry/recovery logic to the shared distribution service.
- Migrated `SteamServerUpdateService` command construction and SteamCMD process startup to the shared distribution service.
- Application composition now creates one shared distribution service for server update and installer workflows.
- Centralized remaining core-service SteamCMD executable-name usage through `ServerPlatformProfile`.
- `ApplicationPathService` workspace/default SteamCMD discovery now uses the platform executable name instead of embedding `steamcmd.exe`.
- Server diagnostics now consume platform process names instead of hard-coded PalServer process names.
- Completed a final Windows platform audit and documented remaining WPF/UI-specific assumptions for the v0.3 Linux foundation.
- No intended changes to current Windows install/update semantics, server lifecycle, MOD evidence, Operational Health, World Inspector safety, or WORLD PULSE.
- Completed a documentation closeout after successful v0.2.16.4 build/runtime validation: removed historical implementation notes from the repository root, archived them under `docs/history/`, moved the current platform audit under `docs/architecture/`, moved publication-process material under `docs/release/`, and rebuilt README/RELEASE_CHECKLIST as current-state documents.
- Updated the public `docs/index.html` baseline/RC wording and Linux-support statement.


## v0.2.16.3 FIX2 — Legacy MystTiq Release-Source State Removal

- Removed the obsolete MystTiq release-source fallback from `FinalizeUpdateCheckResults()`.
- Removed the obsolete status from the generic Update Center action mapper.
- GitHub comparison failures now use the retryable `UNABLE TO CHECK` state.
- Strengthened regression coverage so the legacy state cannot silently return.
- No unrelated runtime behavior changed.


## v0.2.16.3 FIX1 — Update Center Regression Harness False-Positive Hotfix

- Corrected the v0.2.16.3 logic harness so the obsolete manager release-source placeholder check is scoped specifically to the `MystTiq Server Manager` component branch.
- Generic Update Center fallback compatibility text no longer causes a false failure.
- No application source, UI, update behavior, or runtime logic changed.


## v0.2.16.3 — Application Update Awareness & Server Setup Polish

- Added `MystTiqReleaseService` backed by the public GitHub `releases/latest` endpoint for `Wad3M/MystTiq-Palworld-Server-Manager`.
- Added numeric MystTiq version parsing/comparison with explicit `UPDATE AVAILABLE`, `UP TO DATE`, and `DEVELOPMENT BUILD` states.
- Server Setup `CHECK FOR UPDATES` now includes the installed MystTiq version and latest public GitHub release alongside SteamCMD, Palworld, UE4SS, and Workshop information.
- Update Center now performs the same MystTiq GitHub release check and opens the official Releases page when a newer manager release is available.
- GitHub/network failures are fail-soft and do not prevent the remaining component update checks.
- Reduced the Server Environment status badge column, padding, and font size; READY/MISSING/DISABLED/OPTIONAL badges now share compact centered geometry.
- Preserved existing button/theme/tooltip semantics, server lifecycle logic, live-save safety, MOD runtime evidence, Operational Health, and WORLD PULSE behavior.


## v0.2.16.2 — Live Save Read Safety & UI Button Standardization

- Added `SafeWorldSaveSnapshotService` to create stable temporary snapshots of live Palworld save files using `FileShare.ReadWrite | FileShare.Delete`.
- Snapshot creation verifies source length and last-write stability and retries around PalServer save/write windows.
- World Inspector header inspection now reads the stable snapshot instead of opening active `Level.sav` directly.
- Recoverable live-save contention is reported in the Inspector status area without a blocking modal error.
- Hardened `Plm1SaveDecoder` header reads with shared file access for other read-only callers.
- Reduced the shared MystTiq button footprint by approximately 10% while preserving semantic colors, common template behavior, tooltip standards, borders, rounded corners, hover/pressed behavior, and typography hierarchy.
- Normalized Workspace, MOD, Player, Config, icon, and DataGrid button variants to the shared density standard.
- Converted 42 button-only horizontal action clusters to equal-cell `UniformGrid` layouts for consistent grid alignment.
- Removed common per-button size overrides where the semantic/shared style should be authoritative.
- No intended changes to server lifecycle, MOD runtime evidence, Operational Health, World Pulse semantics, save contents, or destructive world-repair behavior.


## v0.2.16.1 — Runtime Path & Deployment Abstraction

- Introduced `IServerPathProfile` and `WindowsServerPathProfile`.
- Centralized deployment/runtime path construction and shared it through the composition root.
- Migrated core UE4SS/MOD/environment/installer/doctor services from direct Win64 path construction.
- Preserved validated Windows behavior; Linux implementation remains deferred.


## v0.2.15.17 — Live World Telemetry & Dashboard Pulse

- Added `WorldTelemetryService` with server-session-scoped player metrics: current online, peak online, joins, leaves, unique players, and last player transition.
- Added `WorldClockProvider` to read the authoritative saved Palworld clock from decoded `Level.sav` `GameTimeSaveData.GameDateTimeTicks`.
- Added `WorldTelemetrySnapshot` / `WorldClockSnapshot` models and centralized telemetry composition.
- Added `ServerService.ActiveSessionId` and `ActiveSessionStartedAt` read-only session metadata for true PalServer uptime.
- Added a Dashboard `WORLD PULSE` strip showing saved world day/time, session uptime, session player metrics, save freshness, latest backup age, and last player event.
- Player join/leave and saved-world day changes can flow into the existing Activity/Audit feed.
- The existing Dashboard UPTIME indicator now reflects the active PalServer session rather than MystTiq process uptime.
- World-clock values are never estimated from uptime; if authoritative save evidence is unavailable, MystTiq displays the clock as unavailable.
- No intended changes to MOD health/runtime evidence, server lifecycle semantics, dark theme, button standards, or tooltip standards.


## v0.2.15.16 — Platform Profile Abstraction & Documentation Consistency

- Added `ServerPlatformProfile` as the centralized source for PalServer process names, executable-relative paths, SteamCMD executable naming, and guarded ports.
- Removed hard-coded Windows PalServer process names and guarded-port constants from `ServerService`.
- `ServerProcessDiscoveryService`, `ServerResourceMonitor`, `ServerSessionInspector`, and `WindowsServerPlatformOperations` now receive their conventions through the selected platform profile.
- Preserved the Windows profile and all current Windows lifecycle behavior as the default.
- Performed a full README consistency rewrite: removed duplicate Feature Matrix/release-diary content, removed stale baseline/RC claims, consolidated MOD runtime-health semantics, corrected Linux wording, and made CHANGELOG/release-notes authoritative for historical version detail.
- No intended UI, dark-theme, button, tooltip, MOD evidence, operational-health, or lifecycle behavior changes.


## v0.2.15.15 — Server Platform Services Abstraction

- Added `IServerPlatformOperations` as the platform boundary for executable resolution, process launch policy, post-launch window handling, forced process-tree termination, and server-process fallback cleanup.
- Added `WindowsServerPlatformOperations` containing the existing Windows-specific implementation.
- `ServerService` now depends on `IServerPlatformOperations` and retains the Windows implementation as its compatibility default.
- Removed Windows `user32.dll` window-hiding P/Invoke and executable-path selection logic from `ServerService`.
- Removed direct `Process.Kill(entireProcessTree: true)` fallback logic from `ServerService`; forced termination now delegates through the platform service.
- Reduced `ServerService` further while preserving the public facade, current session semantics, stream readers, startup/restart behavior, native MOD evidence, and operational-health logic.
- No intended UI, dark-theme, button, tooltip, server configuration, or runtime behavior changes.


## v0.2.15.14 FIX1 — Composition Root Namespace Compile Hotfix

- Added the missing `using PalworldManager.Models;` import to `ApplicationServiceComposition.cs`.
- Restored visibility of `AppSettings` in the new application composition root.
- Added regression-harness coverage for the composition-root namespace dependency.
- No service graph, platform abstraction, UI, MOD health, or runtime behavior changed.


## v0.2.15.14 — Application Composition & Platform Abstraction Preparation

- Added `ApplicationServiceComposition` as an explicit composition root for MystTiq's core server/MOD/diagnostics service graph.
- Removed direct construction of core server/MOD services from `MainWindow`; the window now consumes the composed graph and remains responsible for UI event wiring.
- Added `IServerSessionInspector` as the platform boundary for server-session process tree, loaded-module, descendant-process, and guarded-port inspection.
- `ServerSessionInspector` remains the Windows implementation and preserves v0.2.15.12 session/module behavior.
- `ServerService` now depends on `IServerSessionInspector` while retaining a Windows default implementation for compatibility.
- Established a clean seam for a future Linux session/process inspector without adding OS conditionals throughout server/runtime-evidence logic.
- No intended UI, theme, button, tooltip, health, MOD evidence, start/stop/restart, or server configuration behavior changes.


## v0.2.15.13 FIX2 — Regression Harness Quoting Hotfix

- Fixed PowerShell parsing in `Test-v0.2.15.13-Logic.ps1`.
- Replaced nested double-quoted text in the conflict-regression failure message with PowerShell-safe single-quoted message strings.
- No C# health logic, MOD conflict semantics, UI behavior, or server health scoring changed from FIX1.


## v0.2.15.13 FIX1 — MOD Conflict Health False-Positive Hotfix

- Fixed a server-health false positive where `No known conflict` matched a broad `Contains("Conflict")` check.
- MOD conflict health now requires either `Compatibility == Conflict` or the explicit status `Confirmed conflict`.
- Healthy MODs with `No known conflict` no longer count as confirmed server-health issues.
- Added regression-harness coverage to prevent broad conflict-string matching from returning.
- No change to genuine conflict, missing dependency, runtime error, failed, missing, or misconfigured MOD penalties.


## v0.2.15.13 — Operational Health Model & Application Composition Refinement

- Added `ModPlatformHealthService` as the single source of truth for MOD contribution to server-level health.
- Disabled, Active / Unverified, Active, Installed, and Unknown MOD states are informational/neutral and no longer reduce Overall Health.
- Enabled MOD failures, runtime errors, missing deployment, misconfiguration, state/duplicate attention, confirmed conflicts, and missing dependencies are explicit health issues.
- `DashboardIntelligenceService` now consumes `ModPlatformHealthSnapshot` instead of deriving `installed - healthy`.
- Dashboard MOD card, compact health strip, Overall Health detail, and tooltip now share the centralized MOD health interpretation.
- Preserved v0.2.15.12 FIX1 server lifecycle/process modularization and v0.2.15.10+ runtime evidence behavior.


## v0.2.15.12 FIX1 — Server Inspector Delegation Compile Hotfix

- Repaired three stale `ServerService` references left after process/session inspection was extracted to `ServerSessionInspector`.
- Cleanup now obtains descendant process IDs and guarded listening ports through the extracted inspector.
- Added a public read-only descendant-process query to `ServerSessionInspector`.
- Updated the stale v0.2.15.11 MOD-center source comment to v0.2.15.12.
- Tightened the v0.2.15.12 regression harness to detect stale extracted-helper references before release build.
- No lifecycle behavior, cleanup safety policy, runtime evidence semantics, UI styling, buttons, or tooltips changed.


## v0.2.15.12 — Server Lifecycle & Process Modularization

- Extracted PalServer process-tree, loaded-module, and guarded-port inspection into `ServerSessionInspector`.
- Extracted lifecycle-state interpretation into the pure `ServerLifecycleEvaluator`.
- Reduced `ServerService` while retaining it as the compatibility facade used by the rest of MystTiq.
- Preserved current-session/PID snapshot semantics required by native UE4SS runtime evidence.
- Preserved start, stop, restart, adoption, cleanup, update, resource-monitoring, and I/O behavior.
- Established explicit process/session boundaries that can later be placed behind Windows/Linux platform interfaces.
- No intended UI, theme, button, tooltip, configuration, or runtime-evidence semantic changes.


## v0.2.15.11 FIX1 — MOD Center Extraction Compile Hotfix

- Repaired four interpolated dialog strings in `MainWindow.ModCenter.cs` that were accidentally split across physical source lines during the modularization extraction.
- Restored the original escaped newline behavior (`\n\n`) without changing dialog text or runtime logic.
- Added regression harness checks for the extracted MOD-center dialog strings.
- No MOD architecture, runtime evidence, UI styling, or workflow behavior changed.


## v0.2.15.11 — MOD Architecture Modularization

- Added `ModCoordinator` as the application-facing orchestration boundary for inventory, verification, compatibility, recommendations, and verification export.
- Added `ModDashboardStateService` to own Dashboard projection, merge, health, and summary logic without WPF dependencies.
- Added MOD workflow/result models.
- Extracted the MOD Library/Dashboard/UI workflow from the monolithic `MainWindow.xaml.cs` into `MainWindow.ModCenter.cs`.
- MainWindow now delegates MOD workflow orchestration instead of coordinating backend services directly.
- Preserved runtime evidence, native module verification, UI behavior, button/tooltip standards, and dark theme.
- No intentional user-facing feature or file-format changes.


## v0.2.15.10 FIX1 — Release Synchronization & Harness Hotfix

- Fixed the v0.2.15.10 PowerShell regression harness by replacing the `R` helper name that collided with PowerShell's `r` / `Invoke-History` alias.
- Removed obsolete v0.2.15.9 harnesses from the active `scripts` folder.
- Synchronized `docs/index.html` and `src/PalworldManager/app.manifest` to v0.2.15.10.
- Refreshed release checklist, release notes, build/test plan, apply instructions, and source manifest.
- No native runtime module evidence or MOD verification behavior changed.


## v0.2.15.10 — Native Runtime Module Evidence

- Integrated read-only PalServer process-module evidence for native/hybrid UE4SS mods.
- Exact canonical path matching prevents collisions between mods that both use `main.dll`.
- Native module mapping provides 100% Confirmed Loaded evidence while Confirmed Running still requires functional activity.
- Runtime inspection refreshes the current ServerService session snapshot and includes the PalServer process tree.
- Enumeration failures fail open to Active / Unverified, never a false load failure.


## v0.2.15.9 FIX2 — Native UE4SS Detection & Verification

- Corrected UE4SS inventory classification using actual payload: Lua, Native, Hybrid, or generic UE4SS.
- Added non-executing PE validation and bounded printable-string analysis for native DLL capability hints.
- Native static signatures are diagnostic capability evidence only; they never count as proof that a DLL executed.
- Separated `Active / Unverified` from actual MOD attention/failure counts.
- Dashboard now reports quiet valid MODs as `awaiting runtime confirmation` instead of `need attention`.
- Preserved current-session runtime evidence requirements and observational safety.


## v0.2.15.9 FIX1 — Compile Hotfix

- Added the missing `ConfirmedRunning` member to `RuntimeEvidenceState`.
- Updated the v0.2.15.9 regression harness so PowerShell build steps are judged by exceptions rather than stale `$LASTEXITCODE` values.
- Updated the stale v0.2.15.8 source comment that caused release validation to warn.
- No runtime-verification architecture or functional-detection behavior changed.


## v0.2.15.9 — MOD Functional Verification & Capability Analysis

- Added non-destructive UE4SS capability/source analysis.
- Added Confirmed Running state based on observed current-session functional activity.
- Verification now reports MOD kind, detected runtime APIs, and expected functional proof.
- Preserved v0.2.15.8 FIX1 Active / Unverified semantics for quiet/event-driven mods.
- MystTiq remains observational and does not inject or modify third-party MOD code.


## v0.2.15.8 FIX1 — Runtime Evidence Model & Confidence Engine

- Added Confirmed Loaded, Active / Unverified, Not Loaded, Error, Disabled, and N/A runtime semantics.
- Added evidence confidence, source, matched alias, and explanation.
- Added UE4SS `enabled.txt, starting mod` loader acknowledgement detection.
- Absence of a positive signature no longer falsely means Not Loaded for a correctly deployed/enabled UE4SS mod.
- Preserved current-session evidence boundaries.


All notable public changes will be documented here.

## [0.2.15.8] - 2026-08-09

### Added
- Added `ModRuntimeEvidenceEngine` as the centralized interpreter for UE4SS/Lua positive runtime evidence.
- Added structured evidence explanations with source and matched runtime alias for verification/report diagnostics.
- Added conservative positive UE4SS signatures for Starting/Loading Lua mods and explicit loaded/initialized/registered mod messages.
- Added event-driven runtime-state synchronization so authoritative evidence updates existing MOD Library and Dashboard rows without waiting for a manual Verify All.

### Changed
- RuntimeStateService now delegates positive load-signature extraction to the shared runtime evidence engine instead of owning a single `Starting Lua mod` regex.
- UE4SS verification now consumes the same authoritative runtime state as the MOD Library and treats `LoadedByUe4ss` as positive evidence when the inventory has already resolved a valid runtime identity.
- Runtime verification details now explain whether evidence came from the unified runtime session or unified inventory state.

### Fixed
- Prevented MOD Dashboard rows such as AntiDupe and PalImportFilter from remaining **Runtime Unverified** while the MOD Library already reports them **Loaded**.
- Removed the verification timing window where runtime evidence could arrive after a Dashboard scan but before a later manual refresh.

## [0.2.15.7] - 2026-08-09

### Added
- Added `RuntimeStateService` as the authoritative, session-aware owner for MOD runtime-loaded evidence.
- Added immutable `RuntimeStateSnapshot` records with session ID, revision, timestamps, runtime log identity, loaded aliases, runtime errors, and health metadata.
- Added session-bound UE4SS log offsets so new sessions consume only newly written runtime evidence instead of inheriting historical `Starting Lua mod` lines.
- Added runtime-state diagnostics to the MOD Runtime view, including session ID, revision, loaded alias count, and last observation time.

### Changed
- MOD inventory scans now observe runtime evidence through `RuntimeStateService` and apply the shared snapshot to MOD rows.
- MOD Library refreshes no longer own a private runtime-loaded latch.
- Server session preparation starts a new runtime-state session; server exit clears the authoritative runtime state.
- Runtime verification/export continues to use the same inventory rows, which are now populated from the centralized runtime source of truth.

### Fixed
- Prevented periodic MOD Library refreshes and UE4SS log changes from erasing valid current-session loaded state.
- Prevented runtime-loaded evidence from a previous PalServer session from being carried into a new session.

### v0.2.15.7 FIX1 — Compile hotfix
- Fixed the two-argument `ModService` constructor to chain through its declared `ue4ssResolver` parameter instead of the stale pre-refactor identifier.
- Removed an unused `RuntimeStateService` field from `GenericModVerifier`, clearing the migration warnings without changing verification behavior.
- Added a regression check for the constructor-chain compile failure.

### v0.2.15.6 FIX2 — Runtime-loaded session persistence
- Fixed MOD Library runtime-loaded state reverting to **Not loaded** after UE4SS log rotation/refresh.
- Positive UE4SS load evidence is now latched for the current PalServer session and is not erased by later logs that omit startup lines.
- Session-loaded evidence resets on server exit and before a new server session, preventing stale cross-session status.
- Added regression checks for session-latch application and reset boundaries.

## [0.2.15.6 FIX1] - 2026-08-08

### Fixed
- MOD Library UE4SS/Lua `LOADED` state now refreshes dynamic runtime evidence instead of reusing a pre-start cached resolver snapshot.
- Workshop and managed packages now retain UE4SS runtime-folder aliases so `Starting Lua mod` evidence can match the actual runtime identity even when package/friendly names differ.
- The MOD Library automatically synchronizes after the 45-second startup evidence window.
- Logic regression harness now recognizes the `report.CanStart` gate implementation and no longer treats stale native `$LASTEXITCODE` values as failed PowerShell build steps.

## [0.2.14.9 FIX3] - 2026-08-06

### Changed
- Update Center row actions now use compact sizing that fits the action column.
- Update Center actions now use semantic colors for update, verify, source, install, retry/check, enable/disable, manage, and create operations.

### Fixed
- Admin Commands runtime status now recognizes common package-name and successful-load message variants.
- Admin Commands status now updates from both process output and tailed `Pal.log` content, including when MystTiq adopts an already-running server.

### v0.2.14.9 FIX1
- Added required Windows installer generation and Inno Setup bootstrap tooling.
- Installer assets now publish to `artifacts` with SHA-256 checksums.
- UE4SS runtime compatibility now refreshes from the live MOD inventory after every MOD state change.
- Removed stale hard-coded MOD compatibility guidance.

## [0.2.14.9 FIX2] - 2026-08-06

### Fixed
- Mouse-wheel routing across nested grids, lists, logs, and page-level scroll viewers.
- Diagnostics Center now scrolls correctly when the pointer is over the results grid.
- Parent pages automatically receive wheel input when a nested control reaches its scroll boundary.

## [0.2.14.9] - 2026-08-06

### Added
- Release-candidate validation for repository hygiene, XAML resources, event handlers, version consistency, dialog usage, and release documentation.
- Unified `Build.ps1 Validate` action.

### Changed
- Full release preparation now validates the source tree before build and packaging.

## [0.2.14.8] - 2026-08-06

### Added
- Central application constants for non-user-configurable timing and network defaults.
- Dedicated MainWindow lifecycle partial for startup, timer ownership, cancellation, and shutdown cleanup.
- Window-lifetime cancellation token to stop startup work cleanly when the application closes.

### Changed
- MainWindow constructor no longer contains the complete loaded/closed orchestration logic.
- Monitor, automation, and heartbeat timer subscriptions now have named handlers and are explicitly detached at shutdown.
- Startup and monitor exceptions are isolated, logged, and surfaced through the notification infrastructure without crashing the UI thread.
- Long-lived cancellation sources and disposable services are released through one predictable shutdown path.
- Repeated operational timing values now consume shared constants to reduce configuration drift.

### Safety
- No world, player, guild, base, backup, MOD, or configuration-editing behavior was intentionally changed.
- This release is focused on lifecycle reliability, maintainability, and shutdown safety.

## [0.2.14.5] - 2026-08-06

### Added
- Guided Repair Center status banner, world scan action, repair candidate summary cards, and selection controls.
- Read-only repair-plan workflow with candidate, selected, and high-risk counts.

### Changed
- World Inspector is now the single navigation entry point for world validation and repair workflows.
- Removed the redundant World Validator item from the left navigation.
- Repair Center uses shared MystTiq dark-theme cards, semantic buttons, tooltips, and compact spacing.

## [0.2.14.3] - 2026-08-06

### Added
- Central application dialog facade and expanded dialog service.
- Shared MystTiq button density variants for standard, compact, toolbar, wide, icon, and DataGrid actions.
- Formal UI standards documentation under `release-notes`.

### Changed
- Existing application dialogs now route through one central integration point while preserving current behavior.
- Historical apply notes, build/test plans, and compile hotfix notes now live under `release-notes`.
- Environment DataGrid actions now use the shared DataGrid button standard.

## [0.2.14.2] - 2026-08-05

### Added
- Central player-save discovery and validation service.
- Guarded filesystem enumeration service.
- Ordered startup coordinator with independent stage results.

### Changed
- Players, Guilds, World discovery, World Tools, and Player Recovery now share the same player-save rules.
- Volatile Palworld filesystem scans now tolerate files and folders changing during enumeration.


## [0.2.14.7] - 2026-08-06

### Added
- Diagnostics Center under Tools with read-only application, workspace, server, world, backup, MOD, transaction, and notification checks.
- Provider-based `DiagnosticsService` architecture for registering subsystem health checks.
- Weighted health score with passed, warning, and failed totals.
- JSON and text diagnostics report export.
- Redacted support-package ZIP containing diagnostics, selected configuration metadata, and recent size-limited logs.

### Changed
- Diagnostics and support artifacts are stored in the centralized diagnostics directory for installed and portable modes.
- Transaction Center read-only wording no longer embeds an obsolete version number.


## [0.2.14.6] - 2026-08-06

### Added
- Read-only Transaction Center inside World Inspector.
- Durable transaction and world-import journal discovery.
- Search, filtering, stage details, backup/report links, and rollback-availability reporting.

### Changed
- Transaction History foundation is now an active audit center.

### Safety
- Rollback execution remains disabled until the rollback framework is validated.
- Malformed transaction records are skipped and logged without interrupting history loading.

## [0.2.14.4] - 2026-08-06

### Added
- Dark-themed World Management migration wizard inside World Inspector.
- Progressive seven-step migration workflow with locked, active, and completed states.
- World Inspector tabs for Players, Guilds, Bases, World Validator, World Management, Repair Center, and Transaction History.
- Guided status banner explaining the current migration state and next required action.
- Sidebar World Validator shortcut now opens the validator inside World Inspector.

### Changed
- World Validator is now organized as a World Inspector tab rather than a separate working page.
- Existing repair preview is presented as Repair Center.
- Player, Guild, and Base tabs link to their full management pages while preserving one World Inspector workspace.
- Locked migration stages explain what must be completed before they become available.

## [0.2.14.1] - 2026-08-05

### Added
- Dedicated Workspace Manager page.
- Portable/installed mode visibility and path inspection.
- Workspace validation, folder-opening, browsing, and synchronized path saving.


## [0.2.13.2] - 2026-08-05

### Added
- Portable workspace and application path foundation.
- Automatic PalServer and SteamCMD discovery inside the portable workspace.
- Portable-local settings, logs, cache, notifications, diagnostics, and window state.

### Changed
- Portable packages now include a working directory layout and marker file.
- Installer builds now use non-portable published files.

## [0.2.13.1] - 2026-08-04

### Added
- Central version definition in `Directory.Build.props`.
- Runtime application-version service.
- Shared version reader for build and packaging scripts.
- Release-tag version validation.

### Changed
- Window title, sidebar, user agents, exports, packaging, and installer builds now consume the central version.

## [0.2.12] - 2026-08-04

### Added
- First public open-source baseline.
- Professional one-page operations dashboard.
- Compact CPU and RAM history graphs.
- Live activity ticker and notification center.
- Separate operational-state and overall-health reporting.
- Health breakdown tooltip.
- Standardized MystTiq dark-theme buttons and tooltips.
- Rounded transparent application icon.

### Fixed
- Intentionally stopped servers no longer report poor health solely because they are stopped.
- Live-save temporary files no longer interrupt player and world inspection.
- Notification bell now toggles correctly.
- Clearing the final notification closes the flyout and hides the bell immediately.

## [0.2.14.1 FIX1]
### Fixed
- Backup Center now handles Palworld-exclusive live save locks with an optional coordinated stop-backup-start workflow instead of failing after repeated copy attempts.

## v0.2.14.1 FIX2

### Fixed
- Dashboard guild/base totals now initialize on first launch.
- Players, Guilds, Bases, and guild/base recovery snapshots are preloaded automatically after startup.
- Startup world-data stages fail independently so one unavailable subsystem does not prevent the rest of the application from loading.


## v0.2.14.11
- Finalized Update Center polish
- Admin Commands refresh reliability improvements
- Scroll routing audit
- Documentation and release wrap-up completed

## v0.2.15.1

### Added
- Central `Ue4ssRuntimeResolver` as the authoritative source for UE4SS runtime and MOD-root paths.
- `Ue4ssRuntimeInfo` snapshot model for modern, legacy, active, and runtime-reported MOD roots.
- UE4SS.log parsing for `Loading mods from:` runtime verification.
- Session-cached resolution with explicit refresh and invalidation support.
- Startup diagnostics that log the selected active root, detection method, runtime-reported root, and health state.

### Changed
- v0.2.15.x development now begins from the validated v0.2.14.11 baseline.
- The resolver prefers an existing modern `Win64\ue4ss\Mods` layout, can use UE4SS runtime-log evidence, retains legacy `Win64\Mods` compatibility, and never selects the legacy root merely because it contains more mods.

### Safety
- Phase 1 performs detection and diagnostics only. It does not migrate, move, delete, enable, disable, or rewrite existing MOD files.
## v0.2.15.2

### Changed
- Migrated `ModService` and `ModScannerService` UE4SS runtime operations to `Ue4ssRuntimeResolver.GetActiveModsRoot()`.
- MOD inventory now enumerates the active UE4SS Mods Root rather than assuming `Win64\Mods`.
- ZIP install planning for recognized UE4SS Lua/DLL packages now targets the active runtime root.
- Enable/disable, `mods.txt`, `enabled.txt`, state repair, managed-path detection, and delete operations use the same resolved active root.
- MOD Dashboard folder actions use the resolver-selected active root.
- Server Doctor and UE4SS dependency checks no longer treat a legacy Mods directory alone as proof of the active runtime.

### Safety
- No automatic legacy-to-modern migration or deletion is performed in this phase. Legacy content remains untouched for Phase 3 migration support.
- Existing managed manifests can still recognize both modern and legacy UE4SS paths for identification/migration purposes, while live operations target only the active runtime root.


## v0.2.15.6

### Pre-start MOD Reconciliation & Runtime Health Hardening
- Added `ModLifecycleCoordinator` as the authoritative boundary for normal modded PalServer startup.
- Pre-start reconciliation now repairs UE4SS `enabled.txt` overrides, preserves/creates canonical `mods.txt` entries, and immediately rescans effective MOD state.
- Added a startup health gate that blocks normal modded startup when enabled files are missing, an enabled UE4SS MOD is outside the resolver-selected Active Mods Root, a state mismatch remains, duplicate logical installs are detected, or reconciliation returns filesystem/state warnings.
- Preserved **Start Without MODs** as an intentional health-gate bypass for recovery and isolation testing.
- Added `ModRepairRecommendationEngine` for deterministic operator-facing repair guidance.
- Added TXT + JSON MOD verification report export from the MOD Dashboard using existing MystTiq button/tooltip/dark-theme standards.
- Updated versioning, README roadmap, release checklist, release notes, build/test plan, apply instructions, and source manifest for v0.2.15.6.

## v0.2.15.5

### Centralized MOD Health & Identity
- Added one authoritative `ModHealthEvaluationService` used by verification and UI health presentation.
- UE4SS/Lua mods are no longer reported Healthy without matching runtime load evidence while the server is running.
- Added explicit Runtime Unverified and Misconfigured health states.
- PAK/Workshop mods use installation/enabled verification and do not require UE4SS Lua load evidence.
- Main Dashboard healthy counts now include only genuinely Healthy MODs.
- Workshop display names are resolved from MystTiq's metadata cache during inventory scans so rescans preserve names such as `PalSchema (3625280368)`.
- MOD verification summaries now distinguish healthy, runtime-unverified, misconfigured/attention, disabled, failed/missing, and unknown states.

## v0.2.15.4

### Runtime-Loaded Status & Expanded UE4SS Diagnostics
- Added active-runtime presence and UE4SS-loaded state to managed MOD inventory rows.
- Parse the latest UE4SS runtime log for `Starting Lua mod` evidence.
- UE4SS/Lua mods that are not present under the resolver-selected Active Mods Root now report `Misconfigured` rather than healthy/installed.
- Expanded MOD Runtime diagnostics with UE4SS root, Active Mods Root, Legacy Mods Root, runtime-reported root, path health, active/legacy directory counts, and loaded Lua-mod count.
- Preserved v0.2.15.3 migration semantics; no automatic migration or destructive legacy cleanup was added.

## v0.2.15.3

### UE4SS Legacy Migration & ZIP Normalization — Phase 3
- Added safe copy-first migration from the legacy `Win64\Mods` directory into the resolver-selected Active Mods Root.
- Legacy copies are retained; migration never performs destructive moves or deletes.
- Existing active-root files are preserved when their content differs, and conflicts are reported instead of overwritten.
- Known UE4SS runtime-component folders are skipped so legacy runtime components cannot replace the active UE4SS installation.
- Added a MOD Dashboard migration action using existing MystTiq warning-button and tooltip standards.
- Normalized `ue4ss\Mods\<Mod>`, `Mods\<Mod>`, and wrapped `<Mod>\Scripts\...` archive layouts into `<ActiveModsRoot>\<Mod>\...`.
- Removed the remaining generic ZIP deployment path that could recreate `Win64\Mods` for UE4SS package content.
### v0.2.15.3 FIX1
- Changed the Windows installer default directory for fresh installs to `C:\GameServers\MystTiqPalworldServer`.
- Installer and post-install launch now run elevated; the application itself continues to request administrator privileges on subsequent launches.
- Updated README installer guidance and executable naming.


- Corrected the v0.4.4.1 platform-preservation test so cross-platform `System.Windows.Input.ICommand` usage is not misidentified as WPF.
- Added the missing v0.4.4.1 Linux acceptance and production-readiness scripts required by Linux packaging.
- Windows Avalonia desktop publish now launches the GUI automatically after a successful publish; `-NoGuiLaunch` suppresses launch for CI/release automation.

- v0.4.6.1 connection contract fix: desktop lifecycle DTO now accepts nullable `lastTransitionAt`, matching the headless/Core wire model so a stopped server can establish the management session before its first lifecycle transition.
## v0.4.12.0 — Backup Center Parity

- Adds deep persisted/audited backup verification, explicit restore confirmation, immutable server-side retention preview/apply, and profile-aware backup-root behavior.
- Preserves safety-backup rollback, remote/LAN security, one aggregate status poll, Windows/Linux Avalonia parity, card containment, and centered metallic buttons.
## v0.4.13.0 — MOD Dashboard / MOD Library / UE4SS Parity

- Adds staged, bounded, traversal-safe MOD ZIP installation plus audited selected delete, bulk state, and repair routes.
- Preserves server-side evidence, neutral Disabled/Active-Unverified health, and explicit capability truth.
## v0.4.14.0 — World Inspector Read-only Parity

- Restores ten read-only world evidence sections with headless statistics/integrity metadata, canonical discovery, and remote-safe presentation.
## v0.4.15.0 — World Validator, Recovery & Transaction Center

- Added structural world validation and local report export through the headless API.
- Added bounded, traversal-safe archive Analyze and expiring single-use review plans.
- Added stopped-server, confirmation-gated full-world import and canonical player-save recovery.
- Enforced fresh safety backup, isolated staging, atomic swap, post-validation, rollback, durable journal/audit and GUI refresh.
- Added test-only gated failure injection for every transaction stage; production hosts ignore the test header.
- Kept guild/base binary mutations visibly BACKEND REQUIRED pending a safe Palworld save codec.
## v0.4.16.0 — Activity & Audit + Notifications Parity

- Added Activity/Audit search, severity/category filters and visible-view export.
- Kept persistent audit append-only from the GUI with no silent clear endpoint.
- Added bounded atomic server-side notification persistence and audited read/pin/dismiss/mark-all/self-test routes.
- Added notification badge/page, filters, selection actions and local export without another periodic poll.

## v0.4.17.0 — Crash Analyzer + Palworld Save Tools Parity

- Added bounded evidence-backed crash signature analysis with durable server-side history.
- Added non-destructive isolation guidance that does not claim unproven causes.
- Added on-demand Python, legacy/PlM converter, Oodle and active-save diagnostics.
- Added bounded remote-safe save inventory and read-only signature inspection.
- Preserved single polling, authenticated remote/LAN access, card containment and the protected World Transaction mutation boundary.

## v0.4.17.1 — Documentation Gate Correction

- Synchronized `docs/index.html` and current-version release references after strict validation rejected the v0.4.17.0 candidate.
- No runtime behavior changed from the v0.4.17.0 feature candidate.

## v0.4.17.2 — Promotion-State Gate Correction

- Keeps the roadmap status as Current Release Candidate until the installed-tree gate completes.
- Carries the v0.4.17.1 documentation-index correction and all v0.4.17.0 feature behavior unchanged.

## v0.4.17.3 — Clean/Updater DLL-Release Retry

- Waits for the verified artifact-hosted desktop and sidecar to exit, then retries deletion of only resolved `artifacts`, `bin`, and `obj` directories when Windows briefly retains a DLL mapping.
- Refuses retry deletion for paths outside the project root or for unrecognized directory names.

## v0.4.17.4 — Installed-Workspace Shutdown Guard

- Updater closes MystTiq desktop/sidecar processes from the exact installation target, including development `bin` launches, before invoking clean and replacement.
- Process ownership is constrained by both executable name and a resolved target-root path prefix, with repeated sweeps and exit waits.
