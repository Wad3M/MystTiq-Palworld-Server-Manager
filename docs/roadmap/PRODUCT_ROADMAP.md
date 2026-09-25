# MystTiq Product Roadmap

This document captures forward-looking product direction. Version-specific implementation detail belongs in `release-notes/` and completed history belongs in `CHANGELOG.md` / `docs/history/`.

## v0.3.x — Linux / Headless Platform Foundation

- Cross-platform core and Linux platform services
- Native Linux PalServer lifecycle control
- systemd service hosting and automatic recovery
- persistent headless configuration
- management API foundation
- authentication/TLS security boundary
- automated Linux deployment and acceptance testing
- continued SHARED / LINUX / WINDOWS-BACKPORT discovery

### v0.3.0.7 — Linux Integration & Production Readiness

Final planned v0.3 integration milestone:

- first-run Linux setup automation
- safe upgrade automation preserving configuration/secrets/TLS/server data
- one-command production-readiness/Doctor report
- end-to-end systemd/API/lifecycle/disk/journal integration evidence
- extended acceptance invokes the production-readiness gate
- v0.3.0.8+ reserved only for stabilization/hotfix work if acceptance exposes defects

Primary tested Linux reference: **Ubuntu Server 24.04.4 LTS x86_64**, observed kernel **6.8.0-137-generic**.

## v0.3.1.x — Avalonia Cross-Platform Desktop GUI

**Selected framework: Avalonia UI + .NET 10 + C# + XAML.**

Build one shared MystTiq desktop UI for Windows and Linux.

```text
MystTiq.Desktop (Avalonia)
        ↓
MystTiq Management API
        ↓
Persistent MystTiq service
        ↓
PalServer
```

Closing/crashing the GUI must not stop MystTiq service or PalServer.

## v0.6.1.0 — Advanced Administration, Automation & Analytics Platform

**Accepted source baseline: v0.6.0.0.**

Merges what the grouped roadmap originally split across three milestones (Guardian backups, Automation/RBAC, Analytics) into one pass — see the restructuring note at the top of `docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md`. Adds the app's first background loop (a real Trigger→Condition→Action automation engine), formal backup classes with default retention protection, minimal backward-compatible RBAC layered on the existing bearer token, an Alert Center with disk-space-exhaustion prediction, and a real slice of notification routing. Along the way, retrofits the manual server start/stop/restart routes onto the `OperationCoordinator` — a real gap found where they previously bypassed it entirely. Scoped down from the milestone's full bullet list by design; see `docs/architecture/v0.6.1.0-advanced-administration-automation.md` for the explicit deferred list.

## v0.6.0.0 — Architecture Baseline & Operation Platform

**Accepted source baseline: v0.5.5.0.**

First milestone of the `v0.6.x.0` family (see `docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md` for the full ten-milestone plan this document defers to going forward). Adds a real Operation/Provider/Health/Transaction contract set in `MystTiq.Core/Operations/` and a central `IOperationCoordinator` with resource-key locking, then migrates the three existing destructive-mutation services (World Transactions, Guild Ownership, Base Ownership/Recovery) onto it — proof the architecture is real rather than decorative. Scoped down from the milestone's full acceptance checklist by design; see `docs/architecture/v0.6.0.0-operation-platform.md` for the explicit deferred list.

## v0.5.5.0 — Desktop Shell UI/UX Consolidation

**Accepted source baseline: v0.5.4.0.**

Desktop-only visual/UX pass with no backend or API changes, closing out a round of user-driven visual feedback on the Avalonia shell:

- Sidebar `ToggleButton.nav` template replaced with a bare `ContentPresenter`: FluentTheme's default template painted its own checked/pointerover background directly on an internal part that a plain `Background` setter could not override, bleeding a solid `#0078D4` default-blue frame around the custom rounded glass pill.
- Nav items taller (58px) and the sidebar column wider (260px); the pill now stretches to its full row height (`VerticalContentAlignment="Stretch"` plus explicit `VerticalAlignment="Center"` on the label) instead of centering at its natural text height and leaving dead space above/below.
- Hovering an already-selected nav item now visibly brightens (`ToggleButton.nav:checked:pointerover` with a dedicated lightened gradient) instead of the checked background silently winning with zero visible change.
- Selected/hover glow switched from `BoxShadow` to `DropShadowEffect`: `BoxShadow` was painting a hard rectangular corner past the rounded `CornerRadius` silhouette; `DropShadowEffect` blurs the actual rendered (and correctly clipped) shape.
- Dashboard SERVER card is now a clean three-state traffic light (red = not running, amber = transitioning, green = running), replacing a fourth neutral-grey state that made a stopped server read as calm rather than stopped.
- OVERALL HEALTH label color now follows the same red/amber/green state as its card (`HealthStateColor`) instead of staying hardcoded green through an ATTENTION/red state.
- Every page's previously-duplicated title banner — 18 of them (Server Setup, Update Center, World Explorer, Bases, Guilds, Configuration, Console, Activity & Audit, Backup Center, MOD Dashboard, MOD Library, UE4SS, Diagnostics, Doctor, Workspace, Crash Analyzer, Save Tools, Notifications) — removed from the page body now that the page title/subtitle renders once in a shared card in the ribbon row, which also now carries the Auto refresh checkbox and Connected badge. Frees meaningful vertical space on every page toward fitting full-screen without scrolling.
- Verified via full logic-suite pass (288 checks) and direct visual screenshots on the actual native-resolution display (routed around a 125% primary-monitor DPI-scaling artifact that initially made an unrelated fit-check look broken). No headless/API/platform-specific code touched, so this release does not require the usual Linux runtime verification pass — confirmed by build only.

## v0.5.4.0 — Base Recovery

**Accepted source baseline: v0.5.3.0.**

- Completes the Base ownership-repair work started in v0.5.3.0 by porting the legacy app's `DeleteBaseAndOwnedObjects` operation ("Recover Base") onto `HeadlessBaseOwnershipService`, using the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI pattern.
- Recover Base removes the base's `base_ids` entry from its owning guild and deletes every `base_camp_id_belong_to`-tagged structure/container/worker record outright, rather than reassigning them. Preview decodes the save and reports the exact record count that will be removed before the user confirms.
- Removal only ever discards whole array elements, never a keyed object property: some record shapes (`MapObjectSaveData`) carry the ownership tag several levels below the record's own array element (nested under `Model.value.RawData.value`), and an earlier implementation that deleted the matched inner key directly corrupted that structure and crashed the save encoder. The fix scans each array element's full subtree for the tag and removes the whole element when found.
- The Bases page's "Recover Base — BACKEND REQUIRED" stub is replaced with a real Preview/Apply card carrying an explicit destructive-action confirmation checkbox.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save (copied into an isolated test environment, never against live/production data): Preview reported 132 tagged records for the test base on both platforms, Apply removed all 132 plus the `base_ids` entry, and the result was independently re-decoded (not trusting the service's own report) to confirm zero remaining references and a correct `base_ids` array — identical outcome on both platforms.

## v0.5.3.0 — Base Ownership Transfer

**Accepted source baseline: v0.5.2.0.**

- Ports the ownership-repair pattern proven by Guild Ownership onto Base: `HeadlessBaseOwnershipService` moves a base's `base_ids` entry between guild records and retags every structure/container/worker object placed at that base (confirmed against real save data: 132 tagged records for one base) to the new owning guild.
- Uses the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation and journals into the same transaction store as Guild Ownership and World Transactions.
- Scoped deliberately to Transfer Ownership only; Base Recovery (the legacy app's `DeleteBaseAndOwnedObjects`, which removes tagged objects outright rather than reassigning them) is not ported and remains an honest disabled stub on the Bases page.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save: Apply reported 113 owning-guild tags updated on each platform, independently confirmed by re-decoding the result and checking both guilds' `base_ids` arrays and all 113/113 retagged records — identical outcome both times.

## v0.5.2.0 — Guild Ownership Platform (Promoted Baseline)

**Accepted source baseline: v0.5.1.5.**

- Adds the first working slice of Guild/Base ownership repair: Claim Orphaned Guild, Transfer Leadership, and Add Player to Guild, replacing three disabled "BACKEND REQUIRED" stub buttons on the Guilds page.
- All three operations share one Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation (the same pattern as World Transactions) and journal into the same transaction store.
- Adds `HeadlessSaveCodecService`, a server-side Level.sav Decode/Encode capability that detects each save's real container format (plain PlZ vs. PlM/Oodle) per file rather than assuming one converter — required immediately, since the reference Linux deployment's actual save is PlM/Oodle.
- Fixes `PalworldSettingsConfigurationService.Load()` throwing uncaught and crashing the entire `/api/v1/status/poll` aggregate endpoint when `PalWorldSettings.ini` exists but isn't fully written yet (a real state on a freshly-started PalServer).
- Fixes two version-string literals (`Program.cs` help text, Desktop `Version` property) that had silently drifted from the real build version; both now derive from the assembly instead.
- Base ownership/Palbox repair remains BACKEND REQUIRED — the pattern now exists and is proven, it has not been ported onto Base's different data shape yet.
- Verified end-to-end against a real Linux deployment (Ubuntu 24.04.4 LTS), not just compiled for `linux-x64`.

## v0.5.1.5 — First-Run Setup Wiring and Update Center Correction (Promoted Baseline)

- Restores first-run PalWorldSettings creation through the authenticated headless configuration service.
- Requires explicit confirmation, validates bounded values, refuses active-config overwrite, and preserves official REST/RCON enablement defaults.
- Keeps passwords out of audit details and clears successful wizard secrets from desktop memory.
- Restores Update Center platform, SteamCMD, and PalServer summary visibility.
- Preserves one aggregate status poll, remote/LAN security, card containment, and Windows/Linux Avalonia parity.

## v0.5.1.4 — Legacy Page Detail and Server Tab Correction (Promoted Baseline)

- Corrects the selected server-tab surface and keeps the add-server control immediately beside the tab list.
- Restores Server Setup environment summaries and full component/operation evidence.
- Restores Configuration identity, separate Simple slider/marker controls, and the Advanced default/active table without lifecycle status cards.
- Restores the complete Backup Center command row and archive summaries.
- Keeps the console green and timestamp-first while removing CPU/RAM/thread cards from that page.
- Restores Workspace deployment mode, health, discovery, and complete location controls.
- Preserves the headless-first API boundary, one aggregate poll, secured remote/LAN management, containment and Windows/Linux parity.

## v0.5.1.3 — Navigation and UE4SS Parity Correction (Promoted Baseline)

- Moves Notifications from Home to System while retaining header-bell routing.
- Adds a real top-strip add-server control backed by the connection-profile editor.
- Replaces the redundant UE4SS Installed MODs view with runtime version, health, fork, and server-side evidence.
- Establishes an ordered v0.2.16.4 screen/control comparison, including shared input dimensions, for subsequent parity work.
- Preserves one aggregate status poll, remote/LAN security, global containment and Windows/Linux Avalonia parity.

## v0.5.1.2 — v0.5 Shell Functional Integration (Promoted Baseline)

- Replaces the interim restoration sidebar with the approved v0.5.0.18 visual shell.
- Binds server tabs to real connection profiles and the category/ribbon controls to real navigation and lifecycle commands.
- Hosts every restored functional page inside the v0.5 glass workspace without moving server authority into the desktop.
- Originally placed Notifications under Home; v0.5.1.3 corrects it back to System after detailed legacy comparison.
- Preserves one aggregate status poll, remote/LAN security, global containment and Windows/Linux Avalonia parity.

Supported modes:

- Linux headless only
- Linux desktop + local service
- Windows desktop + local service
- Windows/Linux desktop + secured remote service

### v0.3.1.0 — Avalonia Desktop Foundation (Shipped)

- create `MystTiq.Desktop`
- Avalonia on .NET 10
- Windows x64 + Linux x64
- MVVM foundation
- shared theme/resources
- management API client
- local/remote connection profiles
- connection/security state
- no direct PalServer lifecycle ownership in GUI

### v0.3.1.1 — Shared Shell & Navigation

- working Dashboard / Server / Players / Backups / Mods / Doctor / Settings navigation
- saved local/remote connection profiles
- bearer tokens remain process-memory-only
- optional SHA-256 TLS certificate pinning for known self-signed endpoints
- automated Linux desktop deployment/smoke-test helper


- MystTiq shell/sidebar
- navigation
- service/connection state
- settings shell
- server-selector foundation

### v0.3.1.2 — Dashboard & Lifecycle

- API-backed Start / Stop / Restart
- PalServer readiness/PID/listener evidence
- MystTiq system-service state
- last observed / last transition / uptime
- five-second optional desktop auto refresh
- mutation commands reacquire authoritative service/server state


- status/readiness
- Start / Stop / Restart
- uptime
- CPU / RAM
- health
- activity

### v0.3.1.3 — Logs, Players & Monitoring

- authenticated `/api/v1/players`, `/api/v1/logs/tail`, and `/api/v1/metrics`
- server-side loopback Palworld REST player polling
- bounded PalServer log-tail reads
- PalServer CPU / working-set RAM / thread metrics
- shared Windows/Linux Players and Monitoring pages
- recent in-memory metric history
- graceful unavailable state when Palworld REST is disabled/unconfigured


- live logs
- players
- resource history
- reconnect/refresh behavior

### v0.3.1.4 — Backup & Configuration

- authenticated backup inventory/create/delete/restore API
- restore safety backup and staging/rollback behavior
- validated headless configuration writes with rollback copies
- API/lifecycle/server path/launch-argument editing
- authentication/TLS structures preserved by the service
- shared Windows/Linux Backups and Settings surfaces


- backup workflows
- server/headless configuration
- connection profiles
- local/remote service configuration

### v0.3.1.5 — Production Doctor / Server Doctor

- shared Doctor presentation
- PASS / WARNING / FAIL / UNKNOWN
- evidence
- recommendations
- recheck/export
- safe repair actions where supported

### v0.3.1.6+ — Feature-Parity Expansion

#### v0.3.1.6 — Server Setup & Update Workflows

- server-authoritative SteamCMD/install evidence
- non-mutating SteamCMD plan preview
- API-backed Palworld Dedicated Server update/validation
- shared Windows/Linux Setup & Update UI
- explicit separation of Palworld server updates from MystTiq application updates



#### v0.3.1.7 — World Explorer Foundation

- read-only server-authoritative world discovery
- active-world selection from Level.sav evidence
- bounded world/save file metadata inventory
- player-save and core world-file categorization
- shared Windows/Linux World Explorer UI
- secured-listener lifecycle acceptance uses SKIP rather than WARN when inapplicable

#### v0.3.1.8 — Player & Guild Explorer

- validated player-save identity inventory
- semantic player names and guild membership from authoritative decoded GroupSaveDataMap
- guild leader/member/base-reference evidence
- guild orphan/health review state
- optional live REST enrichment for currently-online players
- shared Windows/Linux Players & Guilds explorer
- read-only behavior; repair/migration remains later

- World Explorer semantic expansion beyond player/guild identities
- guild/player/character tools
#### v0.3.1.9 — MOD & UE4SS Management

- shared MOD inventory and verification API
- PAK `~mods` state
- UE4SS active-root resolver parity
- `mods.txt` authoritative enable state
- runtime Lua load evidence
- neutral Disabled / Active-Unverified health semantics
- stopped-server safe enable/disable state changes
- shared Windows/Linux Avalonia MOD page

- setup/update workflows
- remaining administration tools
- Linux desktop packaging
- Windows/Linux parity/regression

Exact patch boundaries after v0.3.1.5 may move based on implementation evidence.

Keep the proven Windows WPF client during the Avalonia migration; do not perform a big-bang replacement.

## v0.4.x — Windows Service Architecture, Character Migration & Diagnostic Consolidation

### v0.4.0.0 — Windows Persistent Service Foundation (Completed Foundation)

- introduce Windows Service Control Manager integration in shared core
- publish the existing headless host for `win-x64`
- automatic-start service registration with recovery/restart policy
- service status/install/uninstall primitives without WPF ownership
- preserve the proven Linux systemd implementation unchanged
- next: Windows-native lifecycle/session supervision and API hosting under the service

### Windows headless/service architecture

- Windows Service/headless host using the shared core
- WPF UI becomes a client of the persistent background service rather than owning server-management correctness
- low-resource minimized mode with reduced UI-only polling/rendering
- graceful-stop-first lifecycle and explicit force escalation
- service watchdog/recovery, persistent lifecycle state, structured background logging and startup readiness using process + guarded-port evidence
- bring useful Linux discoveries back to Windows throughout the v0.4 line

### Character Migration & Account Transfer

Support account/platform transitions such as **Xbox → Steam** by using an already-created destination character as the new identity while transferring selected gameplay state from the source character.

Migration scope:

- source/destination character identity inspection and confirmation
- level / XP / stats
- inventory and equipped items
- armour, accessories and weapons
- relevant progression / technology state where safe
- owned/carried Pals and associated Pal state
- guild membership
- guild leadership transfer when the source character is guild leader
- update guild references from source identity to destination identity
- conflict detection when destination already has incompatible guild/account relationships

Required safety workflow:

1. analyze/dry-run
2. show migration preview and exact changes
3. automatic pre-migration world/player/guild backup and rollback point
4. perform identity-aware migration rather than blindly replacing the destination save
5. validate resulting character, Pals, equipment, guild references and save integrity
6. produce an exportable migration report
7. only after successful validation offer source-character disposition:
   - Keep Original (default)
   - Archive / Disable Original
   - Reset Original
   - Clear Original
   - Delete Original

The migration engine should live in shared core so Linux/headless tooling can ultimately use the same capability.

### Server Setup / Update UI consolidation

- **Server Setup becomes first-run installation/setup only**
- remove duplicate routine Palworld update controls from Server Setup
- maintain one authoritative ongoing **Update Palworld Server** location/flow
- keep **Update MystTiq** separate and clearly identified as the application update path
- reuse one underlying update service rather than multiple update implementations

### Server Doctor consolidation

Server Doctor becomes the authoritative explanation for Overall Health.

When health is below 100%, Doctor must display:

- every check performed
- PASS / WARNING / FAIL / UNKNOWN state
- evidence for each result
- why health points were deducted
- recommended corrective steps in order
- links/buttons to the appropriate repair/tool
- safe **Fix Automatically** actions where appropriate
- per-check Recheck and full diagnosis
- timestamps and durations
- exportable diagnostic report

No health deduction should exist without a corresponding visible Doctor finding. Informational conditions should not reduce health unless explicitly defined by the health model.

## v0.6.x — Transactional Platform Hardening, Automation & Fleet Management

**Superseded by `docs/roadmap/MystTiq_v0.6_Grouped_Development_Roadmap.md` and its paired `MystTiq_v0.6_Grouped_Logic_Acceptance_Test_Plan.md`.** Those documents are the authoritative, testable breakdown of this phase (10 grouped `v0.6.x.0` milestones with acceptance checklists and a PowerShell logic-test harness under `scripts/Test-v0.6.*.0-Logic.ps1`); this section is kept only as a one-line index so the high-level areas remain discoverable from this file.

Covers, in order (re-grouped 2026-09-03 after v0.6.0.0 shipped — see the restructuring note at the top of the grouped roadmap doc): Architecture/Operation Platform (v0.6.0.0) → Advanced Administration, Automation & Analytics Platform (v0.6.1.0) → **Multi-Server Fleet & Runtime Providers (v0.6.2.0 — pulled forward from the former v0.6.8.0, replacing the standalone "v0.6.x — Multi-Server / Multi-Instance Management" line that used to live in this document)** → Windows Service Hardening, Character Migration & Setup/Update Cleanup (v0.6.3.0) → Troubleshooting & Diagnostics Platform (v0.6.4.0, new) → Provider Framework & Configuration Intelligence (v0.6.5.0) → Player Registry & World Explorer 2 (v0.6.6.0) → Safe World Editing & Recovery (v0.6.7.0) → MOD/UE4SS Platform & Monitoring (v0.6.8.0) → Advanced Intelligence/Remote Clients/Simulation/UX (v0.6.9.0).

Explicit non-goals carried forward: multi-game hosting and commercial billing/hosting-provider features.

Architectural rule: v0.3–v0.5 schemas/APIs/automation/audit records should avoid assuming one server forever so v0.6.8.0 does not require another fundamental rewrite.

## v0.7.x — Themes, Skins & Visual Identity

- centralized theme engine
- MystTiq Dark remains the default/official look
- additional Light, Midnight/AMOLED, Palworld-inspired and high-contrast/accessibility themes
- accent customization
- optional compact/comfortable density
- instant switching and saved preference
- automatic light/dark where appropriate
- theme-aware graphs/status elements while preserving semantic health/warning/failure meaning
- **original Palworld-inspired MystTiq icon family** for Dashboard, Doctor, Players, Guilds, Pals, Worlds/Saves, Backups, Mods, Updates, Automation, Network, Performance, Settings and lifecycle controls
- scalable/high-DPI icon assets, consistent sizes/states and light/dark variants

The icon family should be original MystTiq artwork inspired by Palworld's survival/fantasy visual language rather than copied Pocketpair assets.

## v0.8.x — Adaptive Resource, Performance & Network Optimization

Efficiency release for the application, individual Palworld instances and the host/network as a whole.

### Adaptive server resource policy

- server priority levels such as Critical / High / Normal / Low / Background
- dynamically consider priority, player count, CPU/RAM demand and host capacity
- idle policies such as Always Running / Eco / Aggressive Eco / On Demand
- Active → Idle → Low Resource → Sleeping/Stopped transitions
- save and optional backup before eco shutdown
- active/player-populated servers receive preference over empty/background instances
- safe OS-level controls such as priority/affinity/limits where appropriate; never intentionally starve PalServer below safe operation
- defer/throttle maintenance, backups and updates when higher-priority active servers need resources

### Network efficiency

- per-server and aggregate bandwidth telemetry
- update/download and backup-transfer throttling
- avoid saturating the connection with SteamCMD/backup traffic while populated servers are active
- latency/error/port telemetry where measurable
- prioritize gameplay availability over background MystTiq maintenance where technically practical

### Host + per-server dashboard model

Top-level context tabs should support:

```text
HOST | Main Server | Family | Test | Dev | +
```

**HOST dashboard** shows aggregate CPU, RAM, network, disk, number of running/sleeping servers, total players and separate resource consumption for PalServer instances, MystTiq itself and the operating system.

**Individual server tabs** show that instance's health, players, CPU/RAM/network, uptime, priority/idle state, version/MOD state, charts, lifecycle controls and context-specific Players/Doctor/Backups/Mods/Automation views.

The v0.6 multi-server data model should expose server identity, priority, player count, process, CPU, memory, network, health, operational/idle state and resource policy so v0.8 can add optimization rather than redesigning the fleet model.

## Long-term goal

Reach a stable v1.0 production release after cross-platform parity, safe administration, automation, multi-server operation, visual polish and resource-efficiency hardening are validated.

### v0.4.0.1 — Network Diagnostics & Connectivity Recovery (Previous Candidate)

- Separate Runtime Health from Network Health.
- Resolve effective Palworld UDP game port from launch configuration, with safe 8211 fallback.
- Inspect PalServer process identity and PID-aware UDP socket ownership.
- Treat `0.0.0.0` as `All IPv4 interfaces` and resolve a human-readable LAN endpoint.
- Detect wrong-process port ownership and wrong-port PalServer binding.
- Inspect enabled Windows Firewall rules by effective port/protocol, not only by rule name.
- Add/repair only MystTiq-owned firewall rules without duplicating or silently changing unrelated rules.
- Use startup grace before declaring a missing listener failed.
- Expose diagnostics through the management API and shared Avalonia Diagnostics page.
- Provide controlled restart/recovery and automatic post-restart verification.
- Keep MOD/UE4SS health independent from network-health failures.
- Produce redacted, support-friendly diagnostic reports and structured evidence.

### v0.4.0.2 — Avalonia Navigation & Theme Foundation (Completed)

- Rebuild the left navigation around the original MystTiq hierarchy: Dashboard; SERVER; WORLD; MODS; TOOLS; SYSTEM.
- Preserve working backend-backed pages by routing the new navigation destinations to existing functionality.
- Explicitly label unfinished destinations instead of displaying fake operational data.
- Restore persistent sidebar identity and server/service status.
- Replace the purple prototype with centralized dark navy/blue theme resources and semantic button/card styles.
- Keep Windows and Linux on the same Avalonia shell and MVVM/API/Core architecture.
- Integrate version-specific logic/regression tests into the root Build.ps1 release gate.

### v0.4.3.1 — Dashboard & Local Server Backend Integration (Promoted Baseline)
- Auto-discover local MystTiq/Palworld installation and service/API state without equating API failure with missing installation.
- Import/read the legacy Windows `settings-v2.1.json` configuration for migration continuity.
- Populate the dashboard from real backend contracts and preserve local evidence when the management API is unavailable.
- Includes the Avalonia StringFormat parser correction formerly identified as FIX1.

### v0.4.5.0 — GUI Branding & Navigation Polish (Runtime acceptance incomplete)

The initial v0.4.5.0 build/logic gate passed, but runtime acceptance found missing tray ownership, duplicated page presentation, and Start Server retry/control issues. These remain fixes in the same revision line.


### v0.4.6.8 — Dashboard Meter Containment Fix (Promoted Baseline)

- Constrain dashboard CPU/Memory meters and Resource History drawing to their owning card bounds.
- Preserve v0.4.6.7 tray-gate/reconstruction-roadmap behavior and all prior runtime contracts.

### v0.4.6.7 — Tray Gate Contract Fix & GUI Restoration Roadmap

- Correct stale Tray Lifecycle test contract after v0.4.6.6 build/runtime success.
- Ship the supplied reconstruction guide, 287-event historical inventory and functional crosswalk with the source.
- Establish `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md` as the versioned page-by-page restoration plan.
- Preserve current Avalonia/headless/API architecture, remote/LAN auth/TLS and single status polling.


### v0.4.11.0 — Guilds + Bases Parity (Promoted Baseline)

**Accepted source baseline: v0.4.10.0.** Its strict validation, Windows/Linux builds, runtime smoke, and 160/160 logic gate passed.

- Restore guild search/status filters, export, directory/detail panes, ID copy, leader-player navigation, member evidence, and base references.
- Restore base search/status filters, export, ownership evidence/detail, and ID copy.
- Carry authoritative decoded `base_ids` through the existing headless explorer DTO; never parse remote server saves in the desktop.
- Keep leadership, membership, orphan-claim, ownership repair, and recovery mutations visibly unavailable until the full transaction service exists.
- Enforce Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI for all future guild/base mutations.
- Promotion requires Windows/Linux builds, runtime route checks, read-only/capability-truth tests, single-poll preservation, and remote/LAN security regression coverage.

### v0.4.12.0 — Backup Center Parity (Promoted Baseline)

**Accepted source baseline: v0.4.11.0.** Its strict validation, Windows/Linux builds, runtime smoke, and 170/170 logic gate passed.

- Restore create/refresh/delete plus confirmation-gated restore with stopped-server, safety-backup, staging, and rollback semantics.
- Add deep Verify Selected/Verify All with persisted SHA-256 evidence and privacy-safe audit summaries.
- Add immutable, expiring, single-use retention Preview/Apply; inventory changes require re-preview.
- Open a backup root only for the local profile; remote profiles stay on server-side inventory.
- Promotion requires the complete strict validation, Windows/Linux build, runtime smoke, logic, and installed-tree gate.

### v0.4.13.0 — MOD Dashboard / MOD Library / UE4SS Parity (Promoted Baseline)

**Accepted source baseline: v0.4.12.0.** Its complete source and installed-tree gates passed with 176/176 logic checks.

- Preserve evidence-driven MOD health and neutral Disabled / Active-Unverified semantics.
- Add validated PAK/UE4SS ZIP upload/install, selected delete, enable/disable all, and authoritative state repair.
- Stage and size-bound uploads; reject path traversal before server-side installation.
- Audit every MOD mutation without logging archive contents.
- Keep unsupported Workshop, legacy migration, and runtime-wide UE4SS operations visibly BACKEND REQUIRED.

### v0.4.14.0 — World Inspector Read-only Parity (Promoted Baseline)

**Accepted source baseline: v0.4.13.0.** Its complete source and installed-tree gates passed with 181/181 logic checks.

- Restore Overview, Players, Guilds, Bases, Saves, Statistics, Files, World Explorer, World Health, and Integrity evidence.
- Preserve full canonical World ID plus presentation-only nickname and copy action.
- Add server-side file-category statistics and structural integrity findings.
- Keep discovery canonical-only, exclude backup roots, and prohibit desktop filesystem inspection.

### v0.4.15.0 — World Validator, Recovery & Transaction Center (Promoted Baseline)

**Accepted source baseline: v0.4.14.0.** Its complete source and installed-tree gates passed with 187/187 logic checks.

- Restore active-world validation, findings and report export.
- Add remote-safe archive Analyze → Review Plan → Apply for world import and canonical player recovery.
- Require explicit confirmation, stopped PalServer, fresh safety backup, isolated staging, atomic swap, post-validation, rollback and durable journal/audit.
- Keep guild/base binary mutation visibly BACKEND REQUIRED until a safe save codec exists.

### v0.4.16.0 — Activity & Audit + Notifications Parity (Promoted Baseline)

**Accepted source baseline: v0.4.15.0.** Its complete source and installed-tree gates passed with 196/196 logic checks.

- Restore Activity search, severity/category filtering and visible-view export.
- Keep persistent audit clearing policy-locked from the GUI.
- Add persistent server-side Notifications with read, pin, dismiss, mark-all-read and self-test operations.
- Preserve single periodic polling and authenticated remote/LAN behavior.

### v0.4.17.4 — Crash Analyzer + Palworld Save Tools Parity (Promoted Baseline)

**Accepted source baseline: v0.4.16.0.** Its complete source and installed-tree gates passed with 204/204 logic checks.

- Restore bounded evidence-backed crash analysis, persistent history and safe isolation guidance.
- Never claim causality from successful-load lines or chronological proximity alone.
- Restore server-side Python, legacy/PlM converter, Oodle and active-save diagnostics.
- Keep save inventory and signature inspection read-only and bounded.
- Keep every save mutation behind the existing transactional backup/staging/validation/rollback workflow.

### v0.4.18.2 — Workspace + Diagnostics + Settings Closeout (Promoted Functional Baseline)

**Accepted source baseline: v0.4.17.4.** Its complete source and installed-tree gates passed with 214/214 logic checks.

- Restore Workspace refresh, path syntax validation, local-profile browse/open, and rollback-backed save through the editable configuration API.
- Keep remote server paths out of the GUI computer's shell and filesystem pickers.
- Restore diagnostic copy/export and a redacted local ZIP support package while retaining authenticated network/firewall/listener actions.
- Preserve connection profiles, LAN discovery, process-memory-only bearer tokens, TLS certificate pins and local bootstrap.
- Classify all 287 historical WPF event rows with current mapping and closure evidence.
- Complete the v0.4 restoration sequence and stop before v0.5.0.0.

## v0.5.x — Functional Visual Shell Integration

Use the v0.2.16.4 GUI as the workflow and information-architecture reference while retaining the current Avalonia/headless implementation as the technical authority. Every restored operation must remain View → ViewModel → API client → route → headless service. No GUI-local PalServer, remote-filesystem, credential, or mutation ownership is reintroduced.

### v0.5.1.0 — Shell Navigation + Existing Functionality (Promoted Baseline)

**Accepted functional baseline: v0.4.18.2.** The user-supplied v0.4.17.4 FullSource remains a comparison reference and is not used to downgrade the installed tree.

- Integrate the compact Palworld icon + simple MystTiq wordmark identity.
- Keep World navigation to Inspector, Players, Bases and Guilds, with related transactional tools entered from Inspector.
- Keep Settings, Activity & Audit and Notifications under System.
- Route the top notification bell to Notifications and the gear to Settings.
- Use a glass-highlight hover while preserving card containment and centered metallic button content.
- Retain all restored v0.4.18.2 behavior and its single aggregate status poll.

### v0.5.1.1 — Home Navigation Refinement (Shipped)

- Create a Home section containing Dashboard and Notifications.
- Keep System focused on Settings and Activity & Audit.
- Match the selected Dashboard/Notifications row to the prototype's compact blue glass treatment.
- Preserve the header notification shortcut and all persistent notification API behavior.

### v0.4.7.0+ — Versioned GUI Restoration Program (Next Feature Builds)

See `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md` for the authoritative sequence. Every operational control must trace old visual/control → old handler intent → current ViewModel command → current API client → current API route → current headless service. Missing state-changing functionality is BACKEND REQUIRED; world/save mutations require Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI.

### v0.4.5.1 — Tray Lifecycle, Page Parity & Server Start Reliability (Promoted Baseline)

Runtime-acceptance continuation also restores hover information tooltips, right-click player administration with native REST Kick/Ban and capability-gated provider actions, persistent Activity/Audit logging, Windows PalServer console capture, and canonical world discovery that excludes backup snapshots.

- Preserve the v0.4.4.3 promoted headless/API/runtime architecture.
- Use the established MystTiq logo and window icon in the shared Avalonia desktop.
- Indent expanded navigation child rows modestly while keeping the common fixed navigation height.
- Keep Windows/Linux presentation and behavior aligned.
- Add versioned logic tests covering branding assets, navigation layout, packaging and platform preservation.

### v0.4.4.3 — Server / Configuration / Console / Workspace Integration (Promoted Baseline)
- Restore server operations, configuration, console and workspace capabilities through the headless/API/Core architecture.
- Trace GUI operations through View -> ViewModel -> API/service -> endpoint -> Core -> platform implementation.
- Keep server/service lifecycle independent from GUI lifecycle; closing the GUI must not implicitly stop the server.
- Preserve a shared Avalonia desktop for Windows and Linux with platform abstractions only where required.
- Add behavioral/in-memory tests for ViewModel state transitions and command results wherever practical.

### Post-restoration initiatives (version assignment after v0.4.18.0)

- Save / Client Freeze Diagnostic Monitor
- Save Management, Auto-Save & Save Health
- Advanced Runtime Diagnostics & Incident Bundles
- MOD Validation, Isolation & Functional Testing

These items are retained but must not collide with the v0.4.8.0–v0.4.18.0 GUI restoration sequence.

## Live-Session Backlog

Working convention (established 2026-09-16, direct instruction): while a version is actively in
progress, a new note/ask typed mid-session gets captured here rather than immediately
context-switching to it -- unless it's clarifying the scope of the version already in flight, in
which case it's applied directly instead. Cleared into a real roadmap entry (or picked up outright)
once the current version ships.

- **Post-v0.7.91.0 sequencing** (2026-09-17, direct instruction, following a competitive feature
  review against other Palworld server managers -- researched GitHub/Nexus Mods projects and
  cross-checked every claim against MystTiq's actual code before concluding what's a real gap):
  explicit priority order for upcoming versions, each its own shipped `v0.7.x.0` unless noted:
  1. Crash-Risk Configuration Detection (v0.7.91.0, Doctor flags
     `BuildObjectDeteriorationDamageRate = 0`) -- done, see `CHANGELOG.md`.
  2. **Real gaps**, confirmed genuinely missing after code-checking (not just missing from a
     marketing page), each its own version:
     - DONE in v0.7.92.0: Guild/base location markers plus click-to-teleport on the existing live World Map panel
       (`PlayerMapPointDto`/`PlayerMapPoints`, v0.6.16.0) -- today it plots only online players,
       not guild bases, and has no interaction. (User has already supplied their own map
       background image, so "ship a bundled default map image" is explicitly NOT part of this --
       confirmed 2026-09-17.)
     - DONE in v0.7.93.0: Nexus Mods catalog browsing and install from inside the app (built to
       Nexus's actual API: no search, 10 per list plus lookup by id, Premium-only direct downloads
       with an nxm-link path for free accounts). Open project decision: Nexus's API policy asks
       public-facing apps to contact Nexus support with a test build before release; not yet done.
     - DONE in v0.7.94.0: Kit/starter-package system (named item/Pal loadouts, manual give, and
       auto-gift to players first seen after it is enabled). Delivers through PalDefender's
       giveitems/givepal over RCON behind an IKitCommandRunner seam, so the v0.8.x give-item decision
       (which provider) can plug in without reworking kits. PalDefender's live reply is still unverified.
  3. **Enhancements to features MystTiq already has** (not gaps -- confirmed these already exist in
     code, just less developed than some competitors'):
     - DONE in v0.7.95.0: Discord bot depth. Correction to this note: per-Discord-role permission
       mapping already existed; what was added is a live status message, presence, a join/leave/server-
       state events feed, kick/ban autocomplete, save/backup/unban commands, and no-mentions replies.
       Unverified against real Discord (no bot token available yet).
     - DONE in v0.7.96.0: live map players and bases. Found while answering "i supplied the maps": the
       feature existed but was collapsed, opt-in and mis-calibrated for the expanded map. Now open by default,
       on by default, calibrated (start location + real world data). Follow-ups not done: last-known positions
       of offline players, Pal positions, click-to-calibrate if a future game update moves the map.
     - DONE in v0.7.97.0: Crash Analyzer depth. The headless analyzer was a keyword counter with false
       positives; it now uses a 12-signature catalog with causes and fixes, translates exit codes, names mods
       in the evidence, and separates new from already-reported findings. v0.8.9.0: Unreal crash reports read as evidence, a
       Palworld-specific signature from a real report (TArray engine check), and an alert on a new critical finding from a new
       report. Follow-ups not done: further signatures only as new real evidence appears.
     - DONE in v0.7.98.0: Doctor depth. Disk space on every platform (was Linux-only) incl. next-backup
       headroom, backup freshness vs the world's last change, admin access (empty/common/short password with
       REST or RCON on, never echoed), memory, and unreviewed critical crash findings reaching the Dashboard
       badge. Follow-ups not done: outside-in port reachability probe (v0.8.12.0: second-NAT/CGNAT detection instead; a real UDP probe needs an outside peer), a dedicated Map navigation page (asked
       for 2026-09-21 because the map card on the Players page was not found), offline players' last-known
       positions on the map.
     - DONE in v0.7.99.0: dedicated Map page under World (the map card was hard to find on the Players
       page). Looking at the running app found two long-standing marker bugs (all markers drawn top-left;
       markers off-centre), both fixed. Follow-ups not done: player dots seen live (needs a player online),
       last-known positions for offline players, Pal positions, alerting via Discord on a new critical crash.
     - DONE in v0.7.100.0: map zoom (wheel, drag, click a marker or name) and last-known positions for
       offline players with online/offline marked. Follow-ups not done: de-overlapping labels of players
       standing together, solid online dots seen live (needs a player online), Pal positions, alerting via
       Discord on a new critical crash. v0.8.11.0: Pal positions done (base workers and party Pals from the save, clustered,
       base badge; Palbox Pals counted, not drawn). Follow-ups not done: Pal display names (with the Give Item names item).
     - DONE in v0.7.101.0: crash alerts. The recovery loop now notifies on a crash, recovery, failed restart and
       giving up, with the log analysis. Follow-ups not done: alert settings or muting per profile, the OS-service
       supervisor (service-run) has no alerts, delivery routes not exercised.
     - DEFICIENCIES FOUND by walking the running app (2026-09-21, after v0.7.101.0). ALL SEVEN FIXED in v0.7.102.0:
       1. Configuration marks a phantom "1 unsaved change" on load: a real setting whose value sits below its
          slider minimum (ItemCorruptionMultiplier 0.05, slider minimum 0.1) is silently coerced to the minimum,
          so leaving the page prompts to save, and Save Changes would overwrite 0.05 with 0.1. Data-integrity risk.
       2. Alert Center re-fires a persistent condition every cooldown (30 min default, held in memory so it resets
          on restart), each a pinned Critical: 83 unread notifications on the test server, five identical "Low disk
          space" alerts. Needs fire-once-per-episode with a recovery notice.
       3. Alert Center's low-disk rule (percent of the backup volume) and the Doctor's (2/5 GiB) disagree, so one
          says Critical where the other says Pass.
       4. Diagnostics Center shows a stopped server as "Network: ERROR" with Repair Firewall and Restart Server
          enabled; a stopped server is not a network error.
       5. Backups retention "Keep latest" spinner is too narrow to show "10" (reads "1").
       6. Update Center says "Up to date" when the installed version (0.7.101.0) is newer than the latest published
          release (0.2.16.4); it should say so.
       7. Automation has zero rules on a server whose newest backup is 12 days old: no scheduled backup exists.
     - DONE in v0.7.103.0: Doctor scheduled-backup finding with a one-click nightly rule (Admin only, idempotent).
       Follow-ups not done: a confirmation step, the Admin refusal verified with a real Operator token, reminder
       re-alerts, unpinning an alert on recovery, de-overlapping map labels, player dots seen live.
     - DONE in v0.7.104.0: a pinned Critical alert now unpins itself once its condition recovers, and unpins
       itself if the rule is switched off mid-episode too (that path sends no Resolved notice, so it was the one
       way an alert could stay pinned with no follow-up at all). Follow-ups not done: a confirmation step before
       the Doctor's one-click fix, reminder re-alerts, de-overlapping map labels, player dots seen live, the same
       unpin-on-recovery gap on the crash-recovery "gave up" pinned notice (a separate mechanism with no "back
       up" signal today).
     - DONE in v0.7.105.0: every Doctor Fix button now needs a "Confirmed" checkbox ticked first (tooltip names
       the action), the same pattern Backup Restore already uses. Follow-ups not done: reminder re-alerts,
       de-overlapping map labels, player dots seen live, the crash-recovery unpin gap noted above.
     - DONE in v0.7.106.0: markers whose labels would collide (a base and a nearby player, players sharing a
       spot) now stagger the label text into a readable column instead of overlapping; reacts to zoom. Follow-
       ups not done: reminder re-alerts, player dots seen live, the crash-recovery unpin gap, two dots at the
       exact same screen position still rendering as one.
     - DONE in v0.7.107.0: a condition still true after 24 hours now gets a "Still active: ..." reminder,
       repeated every 24 hours until it clears (never pinned, so it cannot grow the pinned count without
       bound). Follow-ups not done: player dots seen live, the crash-recovery unpin gap, two dots at the exact
       same screen position, a Desktop UI setting for the reminder interval (it is an env var today).
     - DONE in v0.7.108.0: the reminder interval is now a real Alert Center setting ("Reminder every (minutes,
       0 = off)"), not just the v0.7.107.0 env var (which still works, for live testing). Follow-ups not done:
       player dots seen live, the crash-recovery unpin gap, two dots at the exact same screen position.
     - DONE in v0.7.109.0: two markers at the exact same screen position now fan out around the shared point
       instead of stacking invisibly (the residual gap v0.7.106.0 named); real separation is untouched.
       Follow-ups not done: player dots seen live, the crash-recovery unpin gap.
     - DONE in v0.7.110.0: the crash-recovery unpin gap -- when automatic recovery gives up, MystTiq now
       watches (api-run only; service-run's own exit-on-give-up contract is untouched) and unpins the DOWN
       notice, sends a "back up" notice, and resumes real monitoring once the server is running again by any
       means other than the loop itself. Closes the last named item from this whole "enhancements to existing
       features" line; only player dots seen live remains, and that needs a real player online to verify, not
       further building.
     - Any other existing-feature depth items surfaced during that work (Crash Analyzer, Doctor,
       etc.) -- add here as found rather than starting fresh research.
  4. **Pulled back into 0.7.x by the user (2026-09-22)**, each its own `v0.7.x.0`, smallest first:
     - DONE in v0.7.111.0: crash alerts mute and per-profile settings (the open decision since v0.7.101.0). Per
       profile, in that profile's own alert rules: crash alerts on/off, "back up" notices on/off, and a timed
       mute (1 h to 7 days, capped at 30 days) that silences every alert for that server. Both follow-ups are
       now done: service-run crash alerts in v0.8.2.0, pausing delivery routes only in v0.8.4.0 (below).
     - DONE in v0.7.112.0: Give Item on the Players page (was a hardcoded-off stub). PalDefender over RCON,
       the same provider Starter Kits use, through `HeadlessKitService.GiveEntriesAsync`; no live-save write
       access. Follow-ups not done: an item actually arriving for a live player is unverified (no player online here).
       The item-id picker is DONE in v0.8.3.0 (below).
     - DONE in v0.7.113.0: Teleport Points on the Map page, reached by a chat command as below. Acts only for a
       player the REST player list shows online under exactly that name and UserId. Follow-ups not done:
       verification with a real player and real PalDefender chat; showing points on the map. Researched
       2026-09-24 (v0.8.4.0): PalDefender's docs do not say whether tp/getpos use world or in-game map units, and an
       open issue reports getpos giving off coordinates over RCON, so it is not guessed. Plan: when Capture runs for an
       online player, record PalDefender's position together with the REST world position of the same player; that pair
       gives the conversion. Needs one player online.
     - Teleport points players can use from anywhere. User's order of preference: the game's own
       teleport mechanic first, a chat command if that does not work. Researched 2026-09-22: the game
       cannot do it server-side (only `bEnableFastTravel` / `bEnableFastTravelOnlyBaseCamp` exist; "fast
       travel from anywhere" is only available as client-side mods each player installs), so this is a
       chat command: MystTiq reads chat (vanilla `[CHAT] <Name> text`, PalDefender
       `[Chat::Global]['Name' (UserId=...)]: text`) and runs PalDefender's `tp <UserId> <X> <Y>` over RCON.
     - DONE in v0.7.114.0: Multi-user login with individually named accounts (salted PBKDF2 passwords, hashed
       12-hour sessions, lockout, live role changes, audit attribution) riding the existing token/role model;
       the Desktop loads the role on connect. Follow-ups not done: a browser sign-in page (MystTiq has no web
       UI), finer per-role hiding of cards beyond the Admin/Owner gating, a real Desktop sign-in against a
       remote MystTiq.
  4b. **Deficiency report to check after the 0.7.x pass above** (user, 2026-09-22, pasted mid-v0.7.113.0). Checked
     2026-09-23: all six confirmed. Items 1-5 DONE in v0.7.115.0 (see docs/architecture/v0.7.115.0-deficiency-fixes.md;
     checking the live map also found that player names had never rendered, fixed too). Item 6 was deferred by the user ("Skip item 6 for now"),
     then DONE in v0.8.0.0 using the user's own updated artwork (see below). Original items:
     - Map markers and labels can still overlap (reported as reproduced with the app's own code).
     - Crash notifications can stay pinned after a backend (sidecar) restart — the v0.7.110.0 give-up id is
       held in memory only.
     - Recovery can report success when the process starts, before the server is confirmed ready.
     - Operation history and recovery state need stronger persistence.
     - Documentation still carries an outdated release version somewhere, and the full verification script
       depends on an older backup archive (the frozen previous-checkpoint ZIP).
     - Navigation icons lack distinct silhouettes; background artwork loses detail in the shallow header crop.
  5. **v0.8.x additions** (explicitly deferred to that milestone, not its own `v0.7.x.0`; confirmed with the
     user 2026-09-22):
     - DONE in v0.8.0.0: Artwork Refresh -- the user's updated graphics from a v0.7.110.1 artwork build (26 distinct
       navigation icons, day/night art for all seven categories, header in its own layout column), 3-way merged into
       v0.7.115.0 with zero conflicts. Closes deficiency item 6. Follow-ups not done: accent presets still only wash
       the art rather than re-colouring it (per-accent art remains a separate idea, see the theme-art memory).
     - DONE in v0.8.1.0: Ribbon Icons -- the user's vector icons for the ten main Ribbon actions (package kept under
       docs/ribbon-icons/). v0.8.8.0 added the user's three image batches (30 colour tiles, docs/ribbon-icons/images), so
       every Ribbon button has a designed icon except Run Analysis and Preview Plan. v0.8.10.0 replaced all 30 with the user's full
       framed originals (nine batch 3 redesigns) and gave Backup and Doctor the Create/Run Doctor images. v0.8.13.0 added the
       user's last ten (batch 4): every Ribbon button on every page now has an image.
     - DONE in v0.8.2.0: Service Mode Fixes -- first release on the accepted baseline v0.8.1.0 (set by the user 2026-09-23).
       service-run used the wrong game port (always 8211), was double-supervised by its embedded API host, and had no
       alerts or persisted recovery state; all three fixed, with an end-to-end service-mode smoke (see
       docs/architecture/v0.8.2.0-service-mode-fixes.md).
     - DONE in v0.8.3.0: Give Item Picker -- the v0.7.112.0 follow-up. Ids come from the world save itself (item static_id,
       Pal CharacterID), kits and earlier gives, so they match the installed game version; a picker adds them to Give or a
       kit. v0.8.13.0: display names and the game's other items, read from the server's own pak (MystTiq's extractor + the user's
       Python/ooz), also used for Pal names on the map. Follow-ups not done: names in other languages (the tables exist; v0.9).
     - DONE in v0.8.4.0: Pause Discord, Email & Webhooks -- the v0.7.111.0 follow-up. Outside delivery can be paused per
       server while the Notifications page keeps everything; a test notification checks the channels. Follow-ups not
       done: real Discord/email delivery observed live (a localhost webhook was used).
     - DONE in v0.8.14.0: per-role card hiding (hidden when unusable, read-only when only readable, notice; Fleet Actions
       fixed to Operator). Follow-ups not done: Ribbon buttons are not role-gated yet (the server still refuses).
     - DONE in v0.8.15.0: a real remote Desktop sign-in (Windows Desktop -> isolated MystTiq on the Linux VM, pinned TLS)
       as Viewer, Operator and Admin, 19/19; found and fixed a 401/403 refusal being read as a success. Follow-ups not
       done: signing in as an Owner user account; signing in from the Desktop running on Linux; Ribbon role-gating.
     - DONE in v0.8.16.0: theme leftovers -- a per-tab Mode picker (Dark, Light, Midnight true black, High contrast,
       Follow the system, live) and Compact/Comfortable density for every tab. Follow-ups not done: the ~240 decorative
       glow/shadow colours outside the catalogue; Windows' own Contrast themes colours are not read; per-accent art.
     - DONE in v0.8.17.0: the HOST tab (the server machine's processor, memory, disks, network traffic) and per-server
       process priority / eco mode (Windows EcoQoS + below normal; Linux niceness per thread; automatic when no one is
       online). Follow-ups not done: bandwidth limits (next), host history, processor affinity, dedicated HOST art/icon,
       systemd Nice= at install.
     - DONE in v0.8.18.0: bandwidth -- per-server network limits (per player, updates per second) written into the server's
       Engine.ini before every start; the game's real defaults (64 Mbit/s per player; 60 updates on Windows, 20 on Linux)
       read from its pak; an upload estimate. Follow-ups not done: OS-level traffic shaping (needs admin), per-process
       traffic (not counted by the OS without tracing).
     - DONE in v0.8.19.0: every route declares its role (planned as "the Ribbon follows the role"; the audit found 105 of 185
       routes with no role -- RCON, kick/ban, settings, mods open to any signed-in principal -- now secure by default and
       scoped), and the Ribbon follows the signed-in role. Follow-ups not done: per-mod toggles and other page buttons.
     - DONE in v0.8.20.0: host history -- processor, memory and the busiest adapter's upload, one reading a minute kept
       7 days in the fleet folder, charted on the HOST tab (hour, day, week) with averages and peaks.
     - DONE in v0.8.21.0: the Linux service unit sets LimitNICE=-11 (no capability), so eco mode can return to full
       speed; a `service-unit` command prints it; checked by systemd on the VM. Not exercised: a raise through a service
       actually installed with the new unit (needs sudo on the VM). Also: every smoke keeps its own fleet folder
       (new `--fleet-root`), 19 older smokes had used the real one.
     - DONE in v0.8.22.0: the remaining sign-in tests -- an Owner account, the Ribbon gating in the signed-in window (and
       the server agreeing), and the Desktop itself running on Linux (the harness published for linux-x64, run on the
       VM). That run found the window-control icons were empty boxes on Linux (a Windows 11 icon font); they are drawn
       now. Not covered: a real Linux desktop session (window manager, clipboard, file pickers, tray); the run is
       headless.
     - DONE in v0.8.23.0 (user: "work on the smaller items", 2026-09-25): every button follows the role -- all 112 commands
       needing more than Viewer (derived from the routes they call; the gate checks the table against the code) and 10
       code-behind world-edit/mod handlers are disabled below their role on pages, menus and the Ribbon (was: Ribbon and a
       few page buttons). Not done: a "needs the X role" tooltip on disabled page buttons.
     - DONE in v0.8.24.0: processor cores (affinity) on the HOST tab (Windows mask; Linux every thread), and the policy
       applied as soon as a start through MystTiq succeeds -- in place of systemd Nice=, which would also slow the
       MystTiq service and needed a reinstall. Crash signatures: no new real reports since 2026-09-16 (the clone's five
       are kinds already covered), so none added.
     - DONE in v0.8.25.0: theme leftovers -- the 92 decorative colours and 21 shadows outside the catalogue follow every
       mode (Dark unchanged), and Windows contrast themes are read (High contrast and Follow the system use them).
       Not done: Linux desktops' own high-contrast colours (no common API).
     - ACCEPTED BASELINE v0.8.25.0 (user, 2026-09-25: "package this up and set it as a baseline"), replacing v0.8.1.0.
       The HOST tab got the user's own art and icon (2026-09-25). Next: v0.9.0.0, finishing the translation.
       Still needs the user: the GitHub release and contacting Nexus Mods,
       per-accent art (and a night version of the HOST art, if wanted), a real Linux desktop session.
     - PLANNED (user decision 2026-09-24, "continue to iterate through the versions until v0.9.0.0"), the open 0.8.x
       follow-ups: v0.8.19.0 Ribbon buttons follow the signed-in role; v0.8.20.0 host history on the HOST tab;
       v0.8.21.0 the Linux service unit allowed to raise priority again (CAP_SYS_NICE) so eco mode can return to normal;
       v0.8.22.0 the remaining sign-in tests (an Owner account, the Desktop itself running on Linux). Then v0.9.0.0:
       finishing the translation. Not autonomous (need the user): the GitHub release and contacting Nexus Mods, per-accent
       and HOST art.
     - Multi-language UI (i18n) -- STARTED in v0.8.5.0 (finishing it is v0.9): the mechanism (Assets/i18n JSON per language, English fallback,
       live switch, Settings picker, gate key-parity check) plus German and Spanish for the category tabs, navigation and
       page headers. v0.8.6.0: the Ribbon too (English label kept as identity, DisplayLabel shown). v0.8.7.0: the Dashboard's
       labels, buttons, tooltips and number templates (TrFormat). Remaining: status values (Dashboard and elsewhere), other page
       content, dialogs and messages, accessible names; more languages (CJK needs a font check); native-speaker review of de/es.
     - Publishing a GitHub release for the 0.7.x line, and contacting Nexus Mods before any public release --
       both explicitly pushed to v0.8.x rather than 0.7.x by the user, since 0.7.x is still an internal RC
       line.
