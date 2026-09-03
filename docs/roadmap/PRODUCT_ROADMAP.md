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

## v0.6.0.0 — Architecture Baseline & Operation Platform (Current Release Candidate)

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

### v0.3.1.0 — Avalonia Desktop Foundation (Current Candidate)

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

Covers, in order: Architecture/Operation Platform (v0.6.0.0) → Guardian & Transactional Safety (v0.6.1.0) → Provider Framework & Configuration Intelligence (v0.6.2.0) → Automation/Notifications/RBAC (v0.6.3.0) → Player Registry & World Explorer 2 (v0.6.4.0) → Safe World Editing & Recovery (v0.6.5.0) → MOD/UE4SS Platform & Analytics (v0.6.6.0) → Transactional Updates/Discovery/Migration (v0.6.7.0) → **Multi-Server Fleet & Runtime Providers (v0.6.8.0 — replaces the former standalone "v0.6.x — Multi-Server / Multi-Instance Management" line)** → Advanced Intelligence/Remote Clients/Simulation/UX (v0.6.9.0).

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

### v0.5.1.1 — Home Navigation Refinement (Current Release Candidate)

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
