# MystTiq Palworld Server Manager
## Consolidated v0.6 Development Roadmap - Grouped Revision Plan

**Baseline:** v0.5.1.5
**Development family:** v0.6.x.x
**Source:** Consolidated review of MystTiq plus eight external Palworld server-management projects.

> **Renumbering note:** This plan was originally drafted as a `v0.5.x.x` family against a `v0.4.6.0` baseline, before the `v0.5.1.x` "Functional Visual Shell Integration" (Avalonia GUI-parity restoration) line existed. That line has since consumed `v0.5.1.0`–`v0.5.1.5` and is still the current release candidate, so this plan has been promoted wholesale to the `v0.6.x.x` family to avoid colliding with already-shipped version identifiers. Every `v0.5.x.0` milestone header below has been renumbered to `v0.6.x.0` (shifted by one MINOR version, same REVISION-slot order). Every `### Absorbs → Original v0.5.X.0...` line is left unchanged — those cite the older, pre-consolidation 27-milestone plan for historical provenance and are not live version identifiers.
>
> This also supersedes the existing `docs/roadmap/PRODUCT_ROADMAP.md` `v0.6.x — Multi-Server / Multi-Instance Management` section, which is now milestone `v0.6.2.0` below (see the 2026-09-03 restructuring note), and the terse `v0.5.x — Advanced Administration, Automation & Remote Management` wishlist in that same document, which this plan expands into testable detail across `v0.6.1.0`–`v0.6.9.0`. `PRODUCT_ROADMAP.md` has been updated to point here instead of duplicating that scope.

> **Restructuring note (2026-09-03, after v0.6.0.0 shipped):** the original `v0.6.1.0`–`v0.6.9.0` sequence below split admin/automation/RBAC/analytics/backups across four separate milestones (old v0.6.1.0, v0.6.2.0, v0.6.3.0, v0.6.6.0) and deliberately placed Multi-Server Fleet last (old v0.6.8.0), on the theory that fleet support is cheaper once everything else exists in single-server form first. After reviewing the actual codebase state post-v0.6.0.0, the near-term sequence was re-grouped as follows — the milestone count stays at ten (`v0.6.0.0`–`v0.6.9.0`), but the middle of the sequence is reshaped:
> - **New `v0.6.1.0`** merges the admin/automation/RBAC/analytics/backup-retention/guild-player-tooling scope of old v0.6.1.0 (Guardian backups), old v0.6.3.0 (Automation/RBAC), and old v0.6.6.0's analytics half into one milestone — accepted as unusually large, shipped as a real-but-scoped-down foundation per area (same playbook as v0.6.0.0), not decorative coverage of everything.
> - **New `v0.6.2.0`** is Multi-Server Fleet, pulled forward from old v0.6.8.0. This is a conscious trade-off: v0.6.1.0's new automation/RBAC/analytics subsystems will ship single-server-only and need a fleet-awareness pass once v0.6.2.0 lands, rather than being built fleet-aware from day one. Accepted explicitly rather than silently.
> - **New `v0.6.3.0`** covers Windows service/headless hardening, the legacy character/account migration wizard (porting `src/PalworldManager/Services/PlayerRecoveryService.cs` / `PlayerMappingEngine.cs` onto the transactional pattern, the same playbook already used for Guild/Base ownership), and Server Setup/Update Center cleanup — Doctor consolidation is pulled out into new v0.6.4.0 instead.
> - **New `v0.6.4.0`** is a new Troubleshooting & Diagnostics Platform milestone: consolidates the two currently-parallel, non-unified health-evidence models (`HeadlessDoctorService`'s `DoctorReport`/`DoctorCheck` and `HeadlessEnvironmentChecklistService`'s `EnvironmentChecklistItem`) into one authoritative model, and adds **local-PC** (the machine running MystTiq Desktop, not the server) port/firewall/connectivity checking — distinct from the existing server-side `NetworkDiagnosticsService`/`WindowsNetworkDiagnosticsPlatformService`, which only checks the *server's* inbound firewall/listener state. Exact scope of "additional tests" for the admin machine to be refined when this milestone is actually planned.
> - Everything previously numbered `v0.6.2.0` (Providers), `v0.6.4.0` (Player Registry), `v0.6.5.0` (Safe World Editing), `v0.6.6.0` (MOD/UE4SS Platform, minus the analytics half absorbed into new v0.6.1.0), and `v0.6.7.0` (Updates/Discovery, minus the setup-cleanup/migration pieces absorbed into new v0.6.3.0) shift down to new `v0.6.5.0`–`v0.6.8.0` in the same relative order. `v0.6.9.0` (Advanced Intelligence) keeps its number.
> - `### Absorbs` lines below still cite the *original pre-consolidation* v0.5.x numbering for historical provenance (unchanged, per the note above) — they do not reflect this 2026-09-03 regrouping.

## Versioning Rule

- **v0.6.x.0** introduces the next grouped functional milestone.
- **v0.6.x.1, .2, .3...** are reserved for syntax fixes, compile corrections, regression fixes, compatibility adjustments, test repairs, documentation corrections, and minor revisions required to complete that milestone.
- Significant new scope waits for the next **v0.6.(x+1).0** milestone.
- Do not advance until the current milestone's logic, runtime, failure-path, persistence, rollback, and regression gates pass.

Example:

```text
v0.6.1.0  Guardian & Transactional Safety implementation
v0.6.1.1  compile/API correction
v0.6.1.2  crash-loop regression fix
v0.6.1.3  restore rollback correction
v0.6.2.0  next grouped functional milestone
```

# v0.6.0.0 - Architecture Baseline & Operation Platform

### Goal
Establish the v0.6 architecture and make long-running/destructive actions coordinated, observable operations without breaking the v0.5.1.5 baseline.

### Scope
- Freeze v0.5.1.5 as regression baseline.
- Define canonical server/profile identity.
- Define operation, provider, capability, health, event, transaction contracts.
- Central `OperationCoordinator`.
- Persisted Operation IDs and records.
- Resource locking, conflict detection, dependencies, queue and priorities.
- Safe cancellation.
- Unified progress, phases, logs, history and rollback metadata.
- Avalonia Operations Center.
- SteamCMD phase/progress parsing.
- Configuration/state migration rules.
- v0.6 architecture documentation and legacy GUI parity inventory.

### Absorbs
Original v0.5.0.0 and v0.5.1.0.

> **Implementation note (shipped v0.6.0.0):** This milestone's own acceptance checklist above is larger than one pass can responsibly ship. What actually shipped: real, working `OperationId`/`OperationRecord`/`ServerProfileId`/`ICapabilityProvider`/`ServerHealthState` contracts in `MystTiq.Core/Operations/`, a central `OperationCoordinator` with resource-key locking (reject-if-conflicting, not queue-and-wait), and migration of the three existing destructive-mutation services — World Transactions, Guild Ownership, Base Ownership/Recovery — onto it as proof it's real rather than decorative. Deferred, not dropped: priority-ordered queueing, dependency-graph blocking beyond a single resource key, restart-safe reclassification of interrupted operations, SteamCMD phase/progress parsing, and a dedicated full log-tail UI (the existing Operations Center/status text remains the log surface). See `docs/architecture/v0.6.0.0-operation-platform.md` for the full contract and migration writeup; the deferred items are picked up by later `v0.6.x.0` milestones as the features that need them land.

# v0.6.1.0 - Advanced Administration, Automation & Analytics Platform

### Goal
Give administrators real scheduled automation, multi-user secured remote control, historical analytics, and formal backup-tier protection — the platform capabilities the app has been missing since v0.5.x, not just more single-action buttons.

### Scope
- Trigger → Conditions → Actions automation engine, persisted execution records, due-time scheduling, retry/jitter, missed/skipped/cancelled/failed/completed states.
- Warning countdown policies and scheduled announcements; HTTP/webhook, RCON, lifecycle, backup/update/MOD actions gated through the Operation Coordinator.
- Multi-user RBAC: roles, per-server permissions, temporary scoped guest grants, rate limiting/lockout/authentication-abuse auditing — today there is exactly one bearer token and no user/role model at all.
- Notification routing matrix: Desktop, Discord admin/public, webhook, provider-ready email; notification templates.
- Historical analytics: tiered metric retention beyond the current in-memory CPU/RAM/thread history, an Alert Center, disk-space prediction, honest graph gaps/restart markers.
- Formal backup classes (manual/scheduled/emergency/safety) with separate retention policies protecting manual/emergency backups from routine pruning — today `HeadlessBackupService` has create/restore/verify/retention-preview but no class distinction.
- Advanced administration: typed RCON command catalog/explorer, deeper guild/player admin actions building on the already-mature `HeadlessPlayerGuildExplorerService`/`HeadlessGuildOwnershipService`/`HeadlessBaseOwnershipService`/`HeadlessPlayerMetadataService`.

### Sizing note
This merges what was previously three separate milestones' worth of scope (old Guardian-backups, old Automation/RBAC, old Analytics). Ship a real, working slice of each bullet — same "real foundation, not decorative" bar as v0.6.0.0 — and explicitly document what's deferred within each area rather than silently thinning all of them out equally.

> **Implementation note (shipped v0.6.1.0):** What actually shipped: a real Trigger→Condition→Action automation engine (`MystTiq.Core/Automation/`, `HeadlessAutomationService`) — the app's first background loop, a `PeriodicTimer`-driven scheduler — with `DailyTime`/`Interval` triggers and `CreateBackup`/`StartServer`/`StopServer`/`RestartServer`/`SendNotification`/`SendRconCommand` actions, including RCON warning-countdown broadcasts; formal `BackupClass` (Manual/Scheduled/Emergency/Safety) with default `Scheduled`-only retention scoping; minimal, fully backward-compatible RBAC (`MystTiqRole`/`MystTiqPrincipal`, `RequireRole` endpoint filter) layered on the existing single bearer token, which still resolves unchanged to a full-access `LegacyOwner`; an Alert Center with CPU/memory/disk-space thresholds and a real linear disk-space-exhaustion projection, reusing the existing Notification Center rather than a parallel pipeline; honest historical-metrics gap/restart markers; and a real (not decorative) notification-routing slice — an actually-implemented Webhook channel, typed Discord/Email stubs, and templates. A genuine gap was found and fixed along the way: the manual lifecycle routes bypassed the Operation Coordinator entirely (a local semaphore only); they're now retrofitted onto it with new `"lifecycle"`/`"world-mutation"` resource keys. Deferred, not dropped: event-based automation triggers (need the Provider Framework's event emission first), real Discord/Email dispatch, per-server RBAC scoping and temporary guest grants (correctly deferred to v0.6.2.0 once servers-plural exist), persisted/distributed rate-limit state, tiered long-term metric retention, and the typed RCON command catalog (explicitly owned by v0.6.5.0's Provider Framework, not duplicated here). See `docs/architecture/v0.6.1.0-advanced-administration-automation.md` for the full contract writeup and per-area deferred list.

# v0.6.2.0 - Multi-Server Fleet & Runtime Providers

### Goal
Remove single-server and single-runtime assumptions.

### Scope
- Stable server/profile IDs and isolated per-server state — `ServerProfileId.Default` already exists as the seam (added in v0.6.0.0) but is currently threaded through nothing except the Operation Coordinator; this milestone threads it through paths, config, lifecycle, and every API route.
- Compound process identity.
- Fleet dashboard and independent health.
- Backup All, Doctor All and coordinated Update All.
- Scheduler jitter/staggered maintenance (depends on v0.6.1.0's automation engine already existing).
- Per-server permissions and visual identity (depends on v0.6.1.0's RBAC already existing).
- Multi-server Operation Coordinator awareness.
- Runtime providers: Windows native, Linux native, official Pocketpair Docker, optional Windows server via Wine on Linux.
- Runtime-independent Guardian, lifecycle, backup, update, Doctor, automation and operations.
- Docker-specific health/config checks.

### Trade-off accepted
Moved ahead of v0.6.1.0's original position (old v0.6.8.0, last in the family) at explicit user direction. The subsystems v0.6.1.0 ships (automation, RBAC, analytics) will be single-server-only when they land and need a fleet-awareness pass here rather than being built fleet-aware from day one.

> **Implementation note (shipped v0.6.2.0):** What actually shipped: config schema v3 (`HeadlessConfiguration.Servers`, plural, with a `MigrateV2` step wrapping an existing single-server config into one `"default"` entry); `LocalManagementApiHost.Create` now constructs one full existing service graph per configured server profile (`ServerProfileHost`) instead of one graph for the whole process, with every route mapped both at its classic unprefixed path (`"default"` profile only, zero change for pre-fleet clients) and under `/api/v1/servers/{profileId}/...` for every profile; `OperationCoordinator` resource locks partitioned by `(ServerProfileId, resourceKey)` so one server's lock never blocks another; `MystTiqPrincipal.ScopedServerProfileId` for per-server RBAC grants; staggered fleet bulk actions (`POST /api/v1/fleet/{backup,doctor,update}-all`); a runtime-provider seam (`ServerRuntimeKind`, Windows/Linux native only); and a new Desktop Fleet page. A real, related bug was found and fixed: `LocalManagementBootstrapper`'s "reuse an already-running instance" check trusted any compatible-looking process with no identity check — `/healthz` now reports `serverProfileIds` and the bootstrapper verifies the expected profile is present. Deferred, not dropped: Docker/Wine runtime provider implementations (the roadmap's "Runtime providers" bullet — need real environments to verify against), Docker-specific health/config checks, a per-page "current server" selector on existing Desktop pages beyond the new Fleet page, and `--server-id` selection for the direct single-server CLI verbs. See `docs/architecture/v0.6.2.0-multi-server-fleet.md` for the full contract writeup and deferred list.

# v0.6.3.0 - Windows Service Hardening, Character Migration & Setup/Update Cleanup

### Goal
Harden the Windows service/headless path, deliver the long-deferred character/account migration wizard, and finish separating one-time setup from ongoing update flows.

### Scope
- Windows service/headless improvements building on the existing `WindowsServerLifecycleService`/`WindowsServiceManager` foundation (shipped v0.4.0.0).
- Character/account migration (e.g. Xbox → Steam): port the legacy WPF app's `PlayerRecoveryService.cs`/`PlayerMappingEngine.cs`/`PlayerMappingModels.cs` (`src/PalworldManager/Services/`) onto the transactional Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit pattern already proven for Guild/Base ownership — analyze/dry-run, migration preview, pre-migration backup, identity-aware migration (not blind overwrite), post-migration validation, exportable report, and source-character disposition (Keep/Archive/Reset/Delete) per the original `PRODUCT_ROADMAP.md` v0.4.x wishlist.
- Server Setup vs. Update Center cleanup: confirm Server Setup (first-run only) has no duplicate ongoing-update controls now that both are separate nav pages (`NavigationPage.ServerSetup` / `NavigationPage.UpdateCenter` already exist); one authoritative "Update Palworld Server" flow, kept clearly separate from "Update MystTiq".

> **Implementation note (shipped v0.6.3.0):** What actually shipped: `WindowsServiceManager` (an `sc.exe` wrapper that existed since v0.4.0.0 but was never called from anywhere) wired into `Program.cs`'s `service-status`/`service-install`/`service-uninstall`/`service-run` commands, previously hard-gated to Linux only; the Linux-only auto-restart-on-crash loop (`LinuxHeadlessSupervisor`) renamed/generalized to `HeadlessSupervisor` since it never used anything Linux-specific, now shared by both platforms — this is what actually makes `RecoveryBackoffSeconds`/`MaximumRecoveryAttempts`/`RecoveryWindowSeconds` real on Windows for the first time; a real `Microsoft.Extensions.Hosting.WindowsServices`/`AddWindowsService()` integration so `sc.exe stop` gracefully shuts the process down instead of SCM eventually force-killing an unresponsive console app; a real SCM-backed `WindowsSystemServiceStatusProvider`, replacing the previously-hardcoded-false status stub for the service-run path. Character/account migration shipped as a real, working identity/guild-reference migration (`MystTiq.Core.Migration.PlayerMappingEngine` + `HeadlessCharacterMigrationService`, following `HeadlessGuildOwnershipService`'s exact Preview → Safety Backup → Transaction → Validate → Journal/Audit pattern) plus Keep/Archive/Delete source-character disposition — full level/inventory/Pal/equipment transplant is explicitly deferred, since it needs new individual-player-`.sav` decode/encode infrastructure nothing in this codebase has today (confirmed via research: the legacy `PlayerRecoveryService` never implemented real mutation either, and explicitly refused naive file-rename migration as unsafe). Setup/Update Center: a real, confirmed bug fixed — Server Setup's per-row "SteamCMD"/"Palworld Dedicated Server" action and "Install Missing" button silently called the same full SteamCMD update/reinstall route as Update Center's dedicated button, even when clicked on an already-installed row reading "Verify"; Setup now only mutates directly for genuinely-missing components, otherwise navigating to Update Center. Two real bugs were found and fixed via live verification against a real save (not just contract-presence checks): the character-migration preview's `canApply` computation counted an informational identity-match line as a blocking finding, making it permanently false; and the new `CharacterDisposition`/`ServerRuntimeKind`-style enum was initially missing its `JsonStringEnumConverter` attribute. Deferred, not dropped: full gameplay-state transplant in migration, `Reset` disposition (needs a fresh-character encoder), a Windows Service Desktop UI (CLI-only this pass, matching Linux), fleet-aware crash-recovery supervision beyond the `"default"` profile, and cross-server migration. See `docs/architecture/v0.6.3.0-windows-service-character-migration.md` for the full contract writeup and deferred list.

# v0.6.4.0 - Troubleshooting & Diagnostics Platform

### Goal
Make Server Doctor the single authoritative explanation for Overall Health, and extend diagnostics to the administrator's own machine, not just the server.

### Scope
- Consolidate the two currently-parallel, non-unified evidence models into one: `HeadlessDoctorService`'s `DoctorReport`/`DoctorCheck` (Component/State/Evidence/Recommendation) and `HeadlessEnvironmentChecklistService`'s `EnvironmentChecklistItem` (Component/Status/Location/Details/Action) — per the original `PRODUCT_ROADMAP.md` ask, no health deduction should exist without a corresponding visible Doctor finding, with PASS/WARNING/FAIL/UNKNOWN state, evidence, recommended corrective steps, links to the right repair tool, safe Fix-Automatically actions where appropriate, per-check Recheck, timestamps/durations, and an exportable report.
- **New**: local-PC (the machine running MystTiq Desktop, not the server) port and firewall/connectivity checking — can this machine actually reach a configured remote MystTiq server, is outbound connectivity blocked locally, etc. Distinct from the existing server-side `NetworkDiagnosticsService`/`WindowsNetworkDiagnosticsPlatformService`/`LinuxNetworkDiagnosticsPlatformService`, which only check the *server's* inbound firewall rules and listener ownership.
- Additional local-environment tests (exact list to be scoped when this milestone is planned in detail — candidates: .NET/runtime prerequisite checks, local disk space, DNS resolution to the configured remote host, TLS trust chain validation for a pinned remote certificate).

> **Implementation note (shipped v0.6.4.0):** What actually shipped: a thin `HeadlessDiagnosticsService` unifying `HeadlessDoctorService`/`HeadlessEnvironmentChecklistService` (both kept unchanged internally) into one `DiagnosticFinding` list, de-duplicating the 3 facts both independently checked; the Dashboard's Overall Health badge now derives from that unified report instead of computing independently from lifecycle status alone, finally consuming `ServerHealthState` (an enum built in v0.6.0.0 as this exact seam, unused until now); real Fix Automatically for backup-root creation and SteamCMD/PalServer install (reusing the existing distribution-update route), honestly gated elsewhere; per-check Recheck; and genuinely new client-side local-PC diagnostics (staged DNS → TCP → TLS → HTTP probe against the selected connection profile, plus .NET runtime/local disk space checks) that work even when the server is fully unreachable, replacing a single opaque exception message with a specific staged diagnosis. Deferred, not dropped: unifying `WorkspaceHealthText`/`EnvironmentHealthText`/`ModHealth` into the same model (only the Dashboard's named Overall Health badge was in scope), safe headless install APIs for Python/UE4SS/save-tools/C++ toolchain (Fix Automatically only covers what already has one), true per-check-isolated re-execution, and local outbound-firewall rule enumeration. See `docs/architecture/v0.6.4.0-troubleshooting-diagnostics-platform.md` for the full contract writeup and deferred list.

# v0.6.5.0 - Provider Framework & Configuration Intelligence

### Goal
Decouple features from individual Palworld interfaces and make configuration schema-driven, version-aware and safe.

### Scope
- Provider contracts for presence, moderation, world data, chat, whitelist, teleport and commands.
- Native REST, GameData, RCON, save/world, PalDefender and MOD providers.
- Capability discovery and best-provider fallback.
- Provider health: Healthy / Degraded / Unavailable / Misconfigured / Unsupported.
- Typed command schema, RCON catalog and advanced API/command explorer.
- MOD runtime state exposed through providers.
- Central configuration schema with types, defaults, ranges, enums, tooltips, restart requirements, version metadata, deprecated/reserved state and dependencies.
- Cross-setting validation and port-conflict detection.
- Minimal configuration writes.
- Presets, import/export and secret-safe portable profile packages.

### Absorbs
Original v0.5.5.0 and v0.5.6.0.

> **Implementation note (v0.6.5.0, 2026-09-03):** shipped a real, working slice rather than the full 7-capability/6-provider-type surface above, matching this roadmap's own "real foundation per named area, explicit deferrals" discipline. `IPlayerModerationProvider` is the first provider contract, with two real implementations: `HeadlessPalworldAdminService` (the existing REST kick/ban logic, unchanged, now also registered as the "rest" provider) and a new `RconPlayerModerationProvider` using Palworld's Source RCON `KickPlayer`/`BanPlayer` commands. `PlayerModerationCoordinator` tries REST first (today's pre-v0.6.5.0 default, unchanged for REST-only servers) and falls back to RCON when REST is disabled, misconfigured, or genuinely unreachable -- closing a real gap where a server with only RCON enabled (a common configuration) had no working kick/ban path at all. Live verification against an isolated instance found and fixed a real bug this fallback depends on: `HeadlessPalworldAdminService`'s REST HTTP call had no exception handling, so a connection-refused failure threw straight out of the coordinator's loop (HTTP 500) instead of letting it fall through to RCON; now caught and returned as an honest `Supported=true/Success=false` result. Configuration Intelligence ships its first real slice -- cross-setting port-conflict detection (Game/REST/RCON ports, only counting enabled interfaces) merged into the existing v0.6.4.0 unified diagnostics report as a new "Configuration" category finding, so it surfaces on both the Doctor page and Dashboard health with zero Desktop changes required. Deferred, explicitly: presence/world-data/chat/whitelist/teleport/commands provider types; GameData/save/PalDefender/MOD provider implementations; the central configuration schema (types/defaults/ranges/tooltips/version metadata/dependencies); minimal configuration writes; presets and import/export.

# v0.6.6.0 - Player Registry, Activity Intelligence & World Explorer 2

### Goal
Create persistent player intelligence and a performant spatial/world administration surface.

### Scope
- Persistent Player Registry with Steam ID / Player UID mapping.
- First/last seen, sessions, playtime, guild history, bans, notes and flags.
- Multi-source presence reconciliation with confidence.
- Join/leave history and player-count history.
- Activity heatmap, peak analysis and maintenance-window recommendations.
- Searchable chat/activity history and queued offline moderation.
- High-performance Canvas map with pan/zoom/fullscreen/clustering/layers.
- Players, Pals where available, bosses, NPCs, fast travel, dungeons, relics, guild bases.
- Base-radius visualization.
- Guild/base inspectors and abandoned-base detection.
- Global item search and storage/container inspection.

### Absorbs
Original v0.5.10.0 and v0.5.11.0.

> **Implementation note (v0.6.6.0, 2026-09-03):** shipped a real, working slice -- a persistent Player Registry and a small World Explorer 2 addition -- rather than the full surface above. `HeadlessPlayerRegistryService` tracks per-player identity/session history (first/last seen, total sessions, cumulative playtime, and real Steam ID/Player UID mapping captured from live REST player data) using the same "sample on the existing status-poll cadence" pattern `HeadlessHistoricalMetricsService` established in v0.6.1.0, rather than adding a second background timer -- meaning observation only happens while something is actively polling (the same accepted limitation the CPU/RAM history already has). Join/leave transitions are detected and recorded as a bounded, persisted event history, with per-tick playtime accrual capped so a large gap between observations can never be misattributed as playtime. Player-count history itself was already covered by v0.6.1.0's `HeadlessHistoricalMetricsService` (`OnlinePlayers`/`KnownPlayers` samples) and needed no new work. World Explorer 2's abandoned-base detection ships as a cheap, real derivation from the existing guild explorer's orphaned-guild data (any base whose owning guild has no present leader), added to `HeadlessPlayerGuildSnapshot` as `AbandonedBaseIds`. Verified live end-to-end against an isolated instance with a small mock Palworld REST endpoint standing in for a real dedicated server: confirmed Join detection, no duplicate Join on a still-online poll, correct playtime accrual between polls, Leave detection on disconnect, and session-count increment on rejoin. Deferred, explicitly: the high-performance Canvas world map and all its overlays (Pals, bosses, NPCs, fast travel, dungeons, relics, base-radius visualization); activity heatmap, peak analysis and maintenance-window recommendations; searchable chat/activity history and queued offline moderation (no chat-capture source exists anywhere in the codebase); multi-source presence reconciliation with confidence scoring; global item search and storage/container inspection (needs save-decode infrastructure that doesn't exist, same gap noted for full character/inventory migration in v0.6.3.0).

# v0.6.7.0 - Safe World Editing, Administration & Player Recovery

> **Roadmap update (v0.5.2.0, 2026-09-01):** the "one transactional mutation engine" this milestone calls for already exists in narrow form -- `HeadlessGuildOwnershipService` proved the full Preview → Safety Backup → Server-side Transaction → Verify → Journal/Audit pattern for guild ownership mutation (Claim/Transfer/Add Player), including a working server-side save codec with per-save container-format detection (PlZ vs. PlM/Oodle), verified end-to-end on both Windows and Linux. This milestone's job is to generalize that proven pattern to player/Pal/base mutation, not build the pattern from scratch.

### Goal
Provide one transactional mutation engine and build all advanced save administration/recovery on it.

### Scope
- Surgical byte-level EditPlan with replace/insert/delete patches.
- Length/count fixups, conflict/overlap and scope validation.
- Server-stopped and world-mutation permission enforcement.
- Fresh snapshot, verified backup, temporary buffer and strict reparse.
- Structural/semantic/invariant validation.
- Atomic replacement, post-write verification, audit and rollback.
- Player progression/inventory administration.
- Pal editor: nickname, level, skills, souls, talents, condenser, gender, work suitability, heal/revive, clone/delete.
- Game-aware constraints.
- Container resizing and guild/base storage editing.
- Bulk base-Pal operations.
- Provider-based teleport/summon and kits where supported.
- Player identity mismatch detection and UID remapping wizard — related to but distinct from v0.6.3.0's character/account migration wizard: this is low-level save-reference repair the migration wizard can eventually build on, not the same feature.
- Structural Level.sav reference changes, player-save migration and guild/reference validation.
- Integrate legacy repair tools into the same mutation framework.

### Absorbs
Original v0.5.12.0 through v0.5.14.0.

> **Implementation note (v0.6.7.0, 2026-09-03):** shipped two real, working slices rather than the byte-level EditPlan engine and Pal/inventory editor above -- confirmed via research (again, same finding as v0.6.3.0's character migration scope decision) that no Pal/inventory/container struct decoder exists anywhere in this codebase, legacy or new, and building one safely was out of scope for a single lean pass. (1) **Guild Membership Repair**: a `RemoveBrokenMember` operation added to the already-proven `HeadlessGuildOwnershipService` (composition, not a new engine) -- the one legacy-repair capability from `GuildBaseRecoveryService`'s scan findings ("Repair membership": a guild member reference with no matching player save) that wasn't already covered by the existing Claim/Transfer/Base-Transfer/Base-Recovery operations. Reuses the exact Preview → Safety Backup → Server-side Transaction → Encode → Verify → Commit pipeline unchanged. (2) **Player Identity Mismatch Detection**: the read-only diagnostic foundation this roadmap explicitly frames as what a future UID remapping wizard builds on ("this is low-level save-reference repair the migration wizard can eventually build on, not the same feature") -- cross-references v0.6.6.0's Player Registry (real Steam ID/UserId capture) against the existing guild explorer's save evidence to surface two real signals: a Steam ID observed under multiple distinct player identities, and a player observed online with no matching save file. Merged into the existing v0.6.4.0 unified diagnostics report as a new `"Identity"` category, explicitly excluded from the Overall Health rollup since these are advisory/analytical findings, not server-operational problems -- verified live that without this exclusion, the routine and expected "just joined, save not written yet" case would incorrectly flip the Dashboard health badge to Degraded. Verified live end-to-end against an isolated instance using a real copy of production save data with one player's `.sav` deliberately removed (the same "delete one player's save to create a genuine broken-reference scenario" technique v0.6.3.0 used): Preview correctly offered Remove Broken Member only for the dangling reference, Apply staged/encoded/independently re-verified the removal inside the transaction and committed successfully, and both new Identity findings appeared with correct evidence. The already-documented `Level.sav.json` sidecar staleness gap (flagged in v0.6.3.0's architecture doc) was reconfirmed, not newly introduced: the read-side guild explorer doesn't reflect a just-applied mutation until that sidecar is externally regenerated, identical to every prior guild-mutation operation. Deferred, explicitly: the surgical byte-level EditPlan engine and everything built on it (Pal editor, container resizing, bulk base-Pal operations, provider-based teleport/summon/kits), player progression/inventory administration, and the interactive UID remapping wizard itself (detection ships this pass; the guided repair workflow does not).

# v0.6.8.0 - MOD/UE4SS Platform & Monitoring

### Goal
Manage extension lifecycles safely, building on the analytics/monitoring foundation shipped in v0.6.1.0.

### Scope
- Mod load order, dependencies and profiles.
- Release discovery and one-click update.
- Backup, staged install, runtime verification and rollback.
- Nexus integration where appropriate.
- Latest vs tested/recommended vs installed version.
- UE4SS dedicated-server validation.
- PalDefender lifecycle integration.
- MOD runtime-state providers.
- Alert Center integration for MOD/UE4SS-specific health (the general Alert Center itself ships in v0.6.1.0).
- Explainable/reversible/audited optimization advisor.

### Absorbs
Original v0.5.15.0 and v0.5.16.0 (the historical metrics/analytics half of this pairing moved to v0.6.1.0 in the 2026-09-03 restructuring).

> **Implementation note (v0.6.8.0, 2026-09-03):** shipped two real, working slices onto the already-mature `HeadlessModManagementService` (install/delete/enable-disable/repair/Workshop-import/UE4SS-runtime-detection all pre-existed and needed no rework) rather than the full roadmap surface. (1) **MOD backup, staged install & rollback**: the roadmap's "staged install" and "runtime verification" halves already existed; "backup" and "rollback" genuinely did not -- `InstallZipAsync`/`DeleteAsync` had no safety net at all before mutating MOD files. New `CaptureSnapshot`/`RollbackAsync` take a lightweight, package-scoped snapshot (one per (type, package), overwritten on each new mutation -- "undo my last change to this MOD," not full history) before every Install/Delete, correctly distinguishing "restore the prior version" from "this package didn't exist before, so rollback should remove it" via an explicit absent-marker rather than silently no-op'ing either case. (2) **Alert Center integration for MOD/UE4SS-specific health**: the one specifically-named roadmap bullet not yet covered by anything else in the milestone -- a new `ModHealthDegraded` rule on the already-proven v0.6.1.0 `HeadlessAlertCenterService` (composition, evaluated on its existing 60s-throttled tick, no new background loop) fires a real notification naming the specific MOD(s) and their health state when the existing MOD inventory computation reports `Degraded`. Verified live end-to-end against an isolated instance: installed v1 of a fake MOD, overwrote with v2, rolled back and confirmed v1's exact content was restored; installed a brand-new MOD and confirmed rollback correctly deleted it entirely (the absent-marker path); deleted an existing MOD and confirmed rollback correctly restored it; confirmed rollback of a never-touched package fails honestly rather than pretending success; induced a real Misconfigured MOD (a `.ucas` file with no matching `.pak`/`.utoc`) and confirmed the Alert Center's next evaluation tick fired a real notification naming it. Deferred, explicitly: mod load order/dependencies/profiles; release discovery and one-click update against Nexus or Workshop; Nexus integration itself; latest-vs-tested-vs-installed version tracking; PalDefender-specific lifecycle handling (it already surfaces as a regular UE4SS MOD entry through the existing generic scanner -- special-casing one named third-party mod by filename was judged low-value versus the mechanism already being generic); the explainable/reversible/audited optimization advisor.

# v0.6.9.0 - Advanced Intelligence, Remote Clients, Simulation & UX Completion

### Goal
Complete the v0.6 family with version adaptation, integrity intelligence, demand-aware hosting, remote clients, simulation and distribution/UX refinements.

### Scope
- Versioned metadata generation for Pals, stats, skills, items, technologies, EXP, icons, map objects and related game data.
- Metadata diffing and parser/log/config compatibility tests.
- Rule-based explainable anti-cheat/world-integrity scanning.
- Review-first, false-positive-safe enforcement.
- Idle auto-stop with warning and final player recheck.
- Scheduled availability/automatic start and operation/event awareness.
- Web/mobile administration using the existing Management API and server-side RBAC.
- Remote Operations Center, status, players, map, chat/activity, backups, alerts and permitted lifecycle actions.
- Simulation/mock providers for lifecycle, players, crashes, hangs, disk, ports, backup/restore/update/MOD/auth failures.
- Deterministic regression and GUI/service acceptance scenarios.
- Resource-based localization, safe data-only community language packs.
- Portable Windows mode.
- Dashboard customization and final UX consistency.

> Note: the idle/on-demand hosting item in this milestone (Demand-Aware Runtime) overlaps with `docs/roadmap/PRODUCT_ROADMAP.md`'s existing `v0.8.x — Adaptive Resource, Performance & Network Optimization` idle-policy bullets. Reconcile the two when this milestone is scheduled rather than building the same idle-shutdown logic twice.

> **Implementation note (v0.6.9.0, 2026-09-03):** shipped one real, working slice -- Idle auto-stop with warning and final player recheck -- rather than the full sprawling roadmap surface above, which spans several genuinely separate large investments (game-data metadata generation and anti-cheat scanning both need Pal/save-struct decode infrastructure that still doesn't exist anywhere in this codebase, the same confirmed gap noted repeatedly since v0.6.3.0; web/mobile administration and a Remote Operations Center are new client surfaces on the scale of the existing Desktop app; simulation/mock providers and localization are each their own substantial infrastructure investment). Confirmed against `PRODUCT_ROADMAP.md`'s v0.8.x note before building anything: this milestone's bullet asks for a narrow, single-server, opt-in "warn then stop if empty" convenience, genuinely distinct in scope from v0.8.x's future multi-tier Active → Idle → Low Resource → Sleeping/Stopped adaptive fleet-wide resource policy engine -- so building the simple version now does not duplicate or foreclose that later work, as long as it stays scoped this narrowly (documented here so a future v0.8.x session sees this reasoning). New `AutomationTriggerKind.IdleEmpty` on the existing v0.6.1.0 automation engine (`AutomationRule`/`HeadlessAutomationService`) -- continuously-observed live state (0 online players for N minutes) rather than a fixed schedule, deliberately excluded from the existing `NextDueUtc` polling path and evaluated on its own path (`EvaluateIdleRulesAsync`) each tick instead. Reuses the already-proven `ExecuteRuleAsync`/`RunLifecycleActionAsync` execution pipeline completely unchanged for the actual stop, including the already-existing warning-countdown RCON broadcast mechanic -- this milestone only adds the idle-detection layer on top. The roadmap's specifically-named "final player recheck" is a new, real safety check: immediately before the actual stop call (after any warning countdown has finished broadcasting), a live player count is fetched one more time, and the stop is aborted -- not just delayed -- if anyone is online, since a player could join during the countdown itself. Verified live against an isolated instance: created a real IdleEmpty rule via the API and confirmed it round-trips with the correct `idleThresholdMinutes`, confirmed `nextDueUtc` stays `null` through creation, disable, and re-enable (proving it never gets swept into the fixed-schedule poller), and confirmed multiple real 15-second automation ticks ran cleanly with no errors while `idleSinceUtc` correctly stayed `null` because the test server was never in the Running phase. **Full end-to-end verification of the actual idle-to-stop transition was not performed this session** -- Windows readiness detection requires a genuine PalServer process with its UDP game port confirmed open (`WindowsServerLifecycleService`), which no synthetic stand-in process can satisfy, so reaching a real "Running" status to observe idle-time accrual and the final stop firing needs an actual Palworld dedicated server, consistent with the standing "never touch/never risk production" constraint. Deferred, explicitly: everything else in the roadmap scope list above -- versioned game-data metadata and diffing, anti-cheat/world-integrity scanning, scheduled availability/automatic start, web/mobile administration, the Remote Operations Center, simulation/mock providers, deterministic GUI/service acceptance scenarios, localization, portable Windows mode, and dashboard customization.

### Absorbs
Original v0.5.21.0 through v0.5.26.0.

# Recommended Development Sequence

1. v0.6.0.0 - Architecture & Operation Platform (shipped)
2. v0.6.1.0 - Advanced Administration, Automation & Analytics Platform (shipped)
3. v0.6.2.0 - Multi-Server Fleet & Runtime Providers (shipped)
4. v0.6.3.0 - Windows Service Hardening, Character Migration & Setup/Update Cleanup (shipped)
5. v0.6.4.0 - Troubleshooting & Diagnostics Platform (shipped)
6. v0.6.5.0 - Provider Framework & Configuration Intelligence (shipped)
7. v0.6.6.0 - Player Registry & World Explorer 2 (shipped)
8. v0.6.7.0 - Safe World Editing & Recovery (shipped)
9. v0.6.8.0 - MOD/UE4SS Platform & Monitoring (shipped)
10. v0.6.9.0 - Advanced Intelligence, Remote Clients, Simulation & UX (shipped)

**The full ten-milestone v0.6.x.0 family shipped 2026-09-03.** Two direct user-requested additions continued the same v0.6.x.0 versioning convention beyond the original ten-milestone plan:

11. v0.6.10.0 - Clone World & Extended Live Verification (shipped)
12. v0.6.11.0 - Stale-Instance Fix, UI Polish & WAN Reachability Diagnostics (shipped)

Next: v0.7.0.0 (Themes, skins, UI personalization and original Palworld-inspired MystTiq icon set — see `README.md`'s roadmap table; no detailed scope document exists yet for this milestone).

# Programming Rules

1. Management Service remains authoritative; GUI clients do not duplicate management logic.
2. Cached state is display-only; mutation requires fresh authoritative state.
3. Destructive world/save operations require a stopped server unless explicitly proven safe.
4. Destructive operations require a recoverable pre-operation state.
5. Restore, update, repair and save mutation are transactional where possible.
6. External clients never bypass RBAC or Operation Coordinator.
7. Providers expose capabilities/health; UI does not hard-code provider selection.
8. Unknown Palworld data is preserved whenever possible.
9. Prefer surgical save mutation over full reserialization.
10. Reparse and validate after save mutation before commit.
11. Validate archive paths, downloads, external URLs and binaries.
12. Keep secrets out of normal logs, exports, diagnostics and plaintext config when protected storage is available.
13. Persist and audit scheduled actions.
14. All long-running actions use the common operation model.
15. Manual desired state survives GUI/service restart.
16. Multi-server identity never relies only on process name.
17. `.0` builds introduce grouped functionality; final-digit revisions stabilize that milestone.

# v0.6 Completion Goal

By v0.6.9.x, MystTiq should function as a transactional Palworld management platform with resilient lifecycle supervision, safe recovery, persistent automation, provider-based integrations, secure remote administration, player/world intelligence, safe save editing, MOD lifecycle management, multi-server/runtime support, historical analytics, simulation/regression tooling and strong auditability.
