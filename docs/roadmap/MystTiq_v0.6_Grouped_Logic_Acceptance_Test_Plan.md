# MystTiq Palworld Server Manager
## v0.6 Grouped Logic & Acceptance Test Plan

**Purpose:** Prove that each grouped `v0.6.x.0` milestone achieves its intended behavior before advancing.

> **Remapped 2026-09-03** to match the current milestone numbering after the 2026-09-03 restructuring (see the restructuring note at the top of `MystTiq_v0.6_Grouped_Development_Roadmap.md`). The original version of this document (drafted before that restructuring) used the *pre-restructuring* milestone numbers and titles, which no longer match anything that actually shipped — several old milestones were split and merged rather than simply renumbered, so a mechanical find-and-replace would have been wrong. This revision instead reflects what each *current-numbered* milestone actually shipped, cross-referenced against `CHANGELOG.md` and each milestone's `docs/architecture/v0.6.X.0-*.md` doc. Items describing scope that was explicitly deferred (not built) are marked **[Deferred]** rather than presented as tested behavior — every milestone in this family shipped a real, scoped-down foundation per its architecture doc's own "explicit deferrals" list, not full original-plan coverage. The full v0.6.0.0–v0.6.9.0 family shipped 2026-09-03; the executable equivalent of this checklist for each milestone is `scripts/Test-v0.6.X.0-Logic.ps1`, which is authoritative where this document and that script disagree.

## Global Release Gate

Every milestone must pass:
- Build/compile.
- Service and GUI startup.
- Prior-version critical regression suite.
- Persistence/restart behavior where applicable.
- Happy path and failure path.
- Rollback/recovery where applicable.
- Human-readable logs/audit.
- No silent failure.
- Safe cancellation where applicable.
- Configuration/state migration from the previous milestone.
- Runtime smoke through the real service/API path where practical.

Patch revisions `.1`, `.2`, `.3...` remain on the same functional goal until these gates are green.

# v0.6.0.0 - Architecture Baseline & Operation Platform

### Goal Validation
- [x] v0.5.1.5 critical logic and runtime smoke still pass.
- [x] Existing configuration/state loads without loss.
- [x] Canonical Server/Profile ID (`ServerProfileId`) exists and persists.
- [x] Operation, provider/capability, health, event and transaction contracts exist in `MystTiq.Core/Operations/`.
- [x] Long-running actions (start/backup/world-transaction/guild/base) create unique Operation IDs via `IOperationCoordinator`.
- [x] Operation record contains server/profile, type, owner/source, phase, status and timestamps.
- [x] Conflicting operations on the same resource key cannot execute simultaneously.
- [x] Non-conflicting operations (different resource keys) can execute concurrently.
- [x] Blocked operation reports what resource it is waiting for.
- [x] Failed/interrupted operations release locks (`OperationHandle.Dispose`).
- [x] The three existing destructive-mutation services (World Transactions, Guild Ownership, Base Ownership/Recovery) are migrated onto the coordinator, not left as decoration.
- [ ] **[Deferred]** Full Operations Center UI showing phase/progress/logs/history for every operation type — only a subset of operation types surface rich progress UI.
- [x] `docs/architecture/v0.6.0.0-operation-platform.md` documents the explicit deferred list.

# v0.6.1.0 - Advanced Administration, Automation & Analytics Platform

Merges the admin/automation/RBAC/analytics/backup-retention scope originally split across three pre-restructuring milestones (old v0.6.1.0 Guardian, old v0.6.3.0 Automation/RBAC, old v0.6.6.0's analytics half) into one pass, accepted as unusually large and shipped as a real-but-scoped-down foundation per area.

### Automation Engine
- [x] `HeadlessAutomationService` is the app's first background loop (15s `PeriodicTimer`); due rules execute via Trigger→Condition→Action.
- [x] `DailyTime`/`Interval` triggers compute correct next-due timestamps, including day-of-week masking and jitter.
- [x] Conditions (`RequireServerRunning`/`RequireServerStopped`) block actions when false.
- [x] `CreateBackup`, `StartServer`, `StopServer`, `RestartServer`, `SendNotification`, `SendRconCommand` actions all execute through the same coordinator-backed pipeline as a manual admin action.
- [x] Warning-countdown broadcasts fire via RCON before a `StopServer`/`RestartServer` action executes.
- [x] Service restart preserves rule state and due schedule (persisted to `automation/rules.json`).
- [x] Missed/Skipped/Cancelled/Failed/Completed run states persist to `automation/runs.json` and are queryable.
- [ ] **[Deferred]** Event-based triggers (on-crash, on-join) — need provider event emission that didn't exist at v0.6.1.0 time; still not built as of v0.6.9.0.

### Backup Classes & Retention
- [x] `BackupClass` (Manual/Scheduled/Emergency/Safety) is stored per backup via a `classification.json` manifest.
- [x] Retention defaults to pruning `Scheduled`-class backups only; Manual/Emergency/Safety are protected from automatic pruning.

### RBAC
- [x] `MystTiqRole`/`MystTiqPrincipal` + `RequireRole` endpoint filter enforce permissions.
- [x] The existing single shared bearer token still resolves to full-access `MystTiqPrincipal.LegacyOwner` — backward compatible, RBAC store isn't even checked when auth is disabled.
- [ ] **[Deferred]** Fine-grained per-capability permissions (kick vs. ban, backup vs. restore, world read vs. edit) — v0.6.1.0 shipped role-level (Viewer/Operator/Admin/Owner), not per-capability, gating.

### Alert Center & Notifications
- [x] `HeadlessAlertCenterService` evaluates CPU/memory/disk thresholds and linear disk-exhaustion prediction on a throttled (~60s) tick.
- [x] Alert cooldowns prevent duplicate-alert spam per rule key.
- [x] `HeadlessNotificationRoutingService` supports a real Webhook channel; Discord/Email are typed stubs, not shipped.
- [x] Manual `/server/start|stop|restart` routes were retrofitted onto `OperationCoordinator` (resource key `"lifecycle"`) — closing a real gap where they previously bypassed it and could conflict with an in-flight World Transaction/Guild/Base Apply.

# v0.6.2.0 - Multi-Server Fleet & Runtime Providers

Pulled forward from the original plan's last-place Fleet milestone; a conscious trade-off documented in the restructuring note (v0.6.1.0's new subsystems shipped single-server-only and needed a fleet-awareness pass here).

### Fleet Isolation
- [x] `HeadlessConfiguration.Servers` (plural, schema v3) with a `MigrateV2` path — an existing single-server v2 config migrates cleanly to a one-entry v3 fleet with zero behavior change.
- [x] `ServerProfileHost` bundles one full existing service graph per configured profile; every route is mapped both at its classic unprefixed path (`"default"` profile only, backward compatible) and under `/api/v1/servers/{profileId}/...` for every profile.
- [x] `OperationCoordinator` locks are partitioned by `(ServerProfileId, resourceKey)` — verified live: concurrent `Server Start` on two profiles fails independently without cross-blocking.
- [x] `HeadlessRbacService`/`HeadlessAuthAbuseGuardService` stay fleet-wide singletons; `MystTiqPrincipal.ScopedServerProfileId` enforces per-server permission grants.
- [x] `/healthz` reports `serverProfileIds`; the Desktop bootstrapper verifies the expected profile before reusing a "local" connection — closes a real cross-talk bug (a local profile could previously attach to the wrong running instance).
- [x] New Desktop Fleet page lists every profile with independent status; `Backup All`/`Doctor All`/`Update All` stagger via `FleetStaggerSeconds` (default 5s).
- [x] Verified live against a real two-server isolated instance: independent config/backups/lifecycle per profile, correctly staggered `Backup All`, non-cross-blocking concurrent starts.

### Runtime Providers
- [x] `ServerRuntimeKind` (Windows/Linux native) is a real seam wrapping the existing platform-specific lifecycle services unchanged.
- [ ] **[Deferred]** Docker container runtime provider.
- [ ] **[Deferred]** Wine-on-Linux runtime provider.
- [ ] **[Deferred]** Linux deployment/verification of the fleet routes specifically — not performed the session this shipped; carried forward as an open gap across every subsequent v0.6.x.0 checkpoint through v0.6.9.0.

# v0.6.3.0 - Windows Service Hardening, Character Migration & Setup/Update Cleanup

### Windows Service Hardening
- [x] `WindowsServiceManager` (an `sc.exe` wrapper that existed since v0.4.0.0 but was never called from anywhere) is wired into `service-status`/`service-install`/`service-uninstall`/`service-run`, previously hard-gated to Linux only.
- [x] `Microsoft.Extensions.Hosting.WindowsServices`/`AddWindowsService()` integration — `sc.exe stop` gracefully shuts the process down instead of the SCM eventually force-killing it.
- [x] `HeadlessSupervisor` (renamed from `LinuxHeadlessSupervisor` — it had no Linux-specific code) is shared across both platforms, making `RecoveryBackoffSeconds`/`MaximumRecoveryAttempts`/`RecoveryWindowSeconds` real on Windows for the first time.
- [x] `WindowsSystemServiceStatusProvider` replaces a previously-hardcoded-false status stub for the `service-run` path.
- [ ] **[Deferred]** Elevated `sc.exe install/start/stop/uninstall` live-cycle test — needs Administrator elevation; only foreground `service-run` (non-SCM) was verified across the whole v0.6.x.0 family through v0.6.9.0.

### Character/Account Migration
- [x] `PlayerMappingEngine` (ported from the legacy `PlayerRecoveryService`/`PlayerMappingEngine.cs`) + `HeadlessCharacterMigrationService` follow `HeadlessGuildOwnershipService`'s exact Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit pattern.
- [x] Migrates a source player's guild membership/leadership to an already-existing destination player identity within the same live world.
- [x] Keep/Archive source-character disposition ships for real; `Reset` is explicitly rejected with a clear message (needs a fresh-character save encoder this build doesn't have) rather than silently no-op'd.
- [x] Verified live: previewed and applied a real guild-membership migration against an isolated copy of a live save, confirmed via safety-backup hash diff that `Level.sav` genuinely changed, confirmed the migration journal appears in Transaction Center history.
- [ ] **[Deferred]** Full level/XP/inventory/Pal/equipment transplant — needs individual-player-`.sav` decode/encode infrastructure nothing in this codebase (legacy or new) has ever implemented; confirmed absent again at every later v0.6.x.0 milestone through v0.6.9.0.
- [ ] **[Deferred]** Interactive UID-remapping wizard's guided-repair workflow (identity mismatch *detection* shipped later, in v0.6.7.0; the wizard itself did not).

### Setup/Update Center Cleanup
- [x] Fixed a real bug: Server Setup's per-row action and "Install Missing" button silently called the same full SteamCMD update/reinstall route as Update Center's dedicated button, even on an already-installed row. Setup now only mutates directly for genuinely-missing (first-run) components; otherwise it navigates to Update Center.
- [ ] **[Deferred]** Full installation-source discovery (registry, Steam `libraryfolders.vdf`, running-process discovery, duplicate-candidate merging) — Setup/Update Center cleanup was scoped to the one confirmed bug above, not a full discovery-engine rebuild.

# v0.6.4.0 - Troubleshooting & Diagnostics Platform

### Unified Diagnostics
- [x] `HeadlessDiagnosticsService` unifies `HeadlessDoctorService`/`HeadlessEnvironmentChecklistService` (both kept unchanged) into one `DiagnosticFinding` list, de-duplicating the 3 facts both independently checked (SteamCMD/PalServer-executable/BackupRoot existence) — verified live: each fact appears exactly once with correct rollup math.
- [x] `ServerHealthState` (defined at v0.6.0.0, unused until now) finally drives the Dashboard's Overall Health badge instead of it computing independently from raw lifecycle status.
- [x] Real "Fix Automatically" for backup-root creation and SteamCMD/PalServer install; everything else stays honestly gated `BACKEND REQUIRED` rather than a decorative fix button.
- [x] Per-check Recheck re-runs the owning source and returns just the one updated finding.
- [x] `DiagnosticState` gained `Unknown`, appended at the end (not inserted first) — a real near-miss caught before shipping, since inserting it first would have silently relabeled every already-shipped Network Diagnostics result (no `JsonStringEnumConverter`, Desktop switches on raw numeric position).

### Local-PC Diagnostics (new, entirely client-side)
- [x] `LocalDiagnosticsService` runs a staged DNS → TCP → TLS → HTTP probe against the selected connection profile, replacing one opaque exception message with a specific staged diagnosis (confirmed via research this was genuinely new — the existing `NetworkDiagnosticsService` only ever checked the *server's* inbound firewall/listener state).
- [x] Local-machine checks (.NET runtime version, local disk space) render through the same shared finding template.
- [ ] **[Deferred]** Local outbound-firewall rule enumeration — the TCP-connect stage's specific failure already gives a real, actionable signal without needing rule-level introspection.

# v0.6.5.0 - Provider Framework & Configuration Intelligence

### Player Moderation Providers
- [x] `IPlayerModerationProvider` is a real provider contract; `HeadlessPalworldAdminService` (existing REST kick/ban logic, unchanged) and a new `RconPlayerModerationProvider` (Palworld RCON `KickPlayer`/`BanPlayer`) both implement it.
- [x] `PlayerModerationCoordinator` tries REST first (unchanged default for REST-only servers), skips a provider reporting `Unavailable`/`Misconfigured` health, and falls through to the next provider on a genuine execution failure — verified live: REST-disabled server correctly routed kick/ban straight to RCON, closing a real gap where RCON-only servers previously had no working kick/ban path at all.
- [x] A real bug found and fixed during live verification: the REST HTTP call had no exception handling, so a connection-refused failure threw straight out of the coordinator's loop (HTTP 500) instead of letting RCON be tried — fixed with a targeted catch, restoring the intended fallback.
- [x] `whisper`/`promote`/`give-item` remain honestly unsupported by both interfaces, byte-for-byte unchanged.
- [ ] **[Deferred]** Provider types beyond moderation (presence, world data, chat, whitelist, teleport, commands) and provider implementations beyond REST/RCON (GameData, save/world, PalDefender, MOD) — not built as of v0.6.9.0.

### Configuration Intelligence
- [x] First real cross-setting check: port-conflict detection (Game/REST/RCON ports, only counting a port whose interface is actually enabled) merged into the unified diagnostics report as a new `"Configuration"`-category finding — verified live: induced a real collision, confirmed it flipped to Fail with correct evidence, reverted and confirmed it returned to Pass.
- [ ] **[Deferred]** The full central configuration schema (types/defaults/ranges/enums/tooltips/restart requirements/version metadata/dependencies), minimal configuration writes, presets, import/export.

# v0.6.6.0 - Player Registry, Activity Intelligence & World Explorer 2

### Player Registry
- [x] `HeadlessPlayerRegistryService` persists per-player first/last seen, total sessions, cumulative playtime, and real Steam ID/Player UID mapping captured directly from live REST player data.
- [x] Samples on the existing `/status/poll` cadence (the same pattern `HeadlessHistoricalMetricsService` already established) — no new background timer.
- [x] Join/leave transitions logged as a bounded, persisted event history; playtime accrual capped per observation tick so a large gap between polls can't be misattributed as playtime.
- [x] Verified live end-to-end using a mock Palworld REST endpoint (since no live PalServer was available): real Join detected once, no duplicate Join while still online, correct playtime accrual between polls, Leave detected on disconnect, session count incremented correctly on rejoin with cumulative history preserved.
- [x] Player-count/known-player history needed no new work — already covered by v0.6.1.0's `HeadlessHistoricalMetricsService`.
- [ ] **[Deferred]** Multi-source presence reconciliation with confidence scoring — today's registry trusts REST player data directly; no save-file cross-reference confidence model.
- [ ] **[Deferred]** Searchable chat/activity history and queued offline moderation — confirmed via research that no chat-capture source exists anywhere in the codebase (neither Palworld's REST API nor RCON expose a chat feed).
- [ ] **[Deferred]** Activity heatmap, peak-time aggregation, maintenance-window recommendations.

### World Explorer 2
- [x] `AbandonedBaseIds` added to the existing guild/base explorer snapshot — bases owned by a guild already flagged `"Orphaned / Needs Review"`, a cheap real derivation from data already computed.
- [ ] **[Deferred]** The high-performance Canvas world map and every overlay (Pals, bosses, NPCs, fast travel, dungeons, relics, base-radius visualization, pan/zoom/fullscreen/clustering/layers).
- [ ] **[Deferred]** Global item search and storage/container inspection — needs save-decode infrastructure that doesn't exist.

# v0.6.7.0 - Safe World Editing, Administration & Player Recovery

### Guild Membership Repair
- [x] `RemoveBrokenMember`, a new operation on the already-proven `HeadlessGuildOwnershipService` (alongside `ClaimOrphanedGuild`/`TransferLeadership`/`AddPlayerToGuild`), removes a guild member reference with no matching player save file — reusing the exact Preview → Safety Backup → Transaction → Encode → Verify → Commit pipeline unchanged.
- [x] Preview correctly rejects the operation for a player who *does* have a valid save, and for a player not actually a member of the target guild.
- [x] Verified live against a real copy of production save data with one player's `.sav` deliberately removed to engineer a genuine broken-reference scenario: Preview correctly offered the operation, Apply staged/encoded/independently re-verified the removal inside the transaction and committed successfully (after one transient Windows file-lock retry — every stage through independent re-verification had already succeeded before that point).
- [x] Reconfirmed the already-documented `Level.sav.json` sidecar-staleness gap (from this same milestone's testing) — the read-side guild explorer doesn't reflect a just-applied mutation until that sidecar is externally regenerated, identical to every other guild-ownership operation, not a defect specific to this one.
- [x] This closes the one legacy-repair gap (`GuildBaseRecoveryService`'s old "Repair membership" finding) not already covered by `ClaimOrphanedGuild` (claim) or `HeadlessBaseOwnershipService` (base reassignment/recovery, already shipped earlier).

### Player Identity Mismatch Detection (read-only foundation)
- [x] Two new `"Identity"`-category diagnostic findings: Steam ID Collision (one Steam ID observed under multiple distinct player identities) and Missing Save File (a player observed online via REST with no matching save), built on v0.6.6.0's Player Registry cross-referenced against existing guild-explorer save evidence.
- [x] Identity findings are explicitly excluded from the Overall Health rollup — verified live that without this exclusion, the routine "just joined, save not written yet" case would incorrectly flip the Dashboard badge to Degraded.
- [ ] **[Deferred]** The interactive UID-remapping wizard's guided-repair workflow — this milestone ships detection only, exactly as the roadmap explicitly framed it ("the migration wizard can eventually build on, not the same feature").

### Byte-Level Editing (not attempted)
- [ ] **[Deferred]** The surgical byte-level EditPlan engine (replace/insert/delete patches, length/count fixups, conflict/overlap validation) and everything built on it: Pal editor, container resizing, bulk base-Pal operations, provider-based teleport/summon/kits, player progression/inventory administration. Confirmed via research that no Pal/inventory/container struct decoder exists anywhere in this codebase, legacy or new — building one safely was out of scope for a single lean pass.

# v0.6.8.0 - MOD/UE4SS Platform & Monitoring

Research at the start of this milestone found `HeadlessModManagementService` already far more mature than the original plan implied — install/delete/enable-disable/repair, PAK + UE4SS scanning with real UE4SS.log runtime-load evidence, and Steam Workshop scan/import all pre-existed and needed no rework.

### MOD Backup, Staged Install & Rollback
- [x] `CaptureSnapshot`/`RollbackAsync` (new) take a lightweight, package-scoped snapshot before every Install/Delete — one per `(type, package)`, overwritten on the next mutation ("undo my last change," not full history).
- [x] Correctly distinguishes "restore prior content" from "this package didn't exist before, so rollback should remove it" via an explicit absent-marker rather than silently no-op'ing either case.
- [x] Verified live: overwrite-then-rollback restored exact prior content; fresh-install-then-rollback correctly deleted a MOD that never existed before; delete-then-rollback correctly restored it; rollback of a never-touched package failed honestly rather than pretending success.
- [x] Existing staged-install (extraction before commit) and runtime verification (UE4SS.log evidence-reading) needed no rework — they already existed.
- [ ] **[Deferred]** Rollback of a nested Workshop-folder-installed PAK MOD or a UE4SS folder-based MOD was not separately exercised this session — only the flat-file PAK path and the absent-marker path were tested against real files (the folder-restore code shares the same already-proven `ZipFile` calls, but wasn't click-tested).
- [ ] **[Deferred]** Mod load order, dependencies and profiles; release discovery and one-click update; Nexus integration; latest-vs-tested-vs-installed version tracking; PalDefender-specific special-casing (it already surfaces through the existing generic UE4SS scanner like any other MOD).

### Alert Center Integration for MOD/UE4SS Health
- [x] New `ModHealthDegraded` rule on the existing v0.6.1.0 `HeadlessAlertCenterService` (composition, same throttled tick, no new background loop) fires a real notification naming the specific MOD(s) and health state whenever the existing MOD inventory computation reports `Degraded`.
- [x] Verified live: induced a real Misconfigured MOD (a `.ucas` file with no matching `.pak`/`.utoc`) and confirmed a real notification fired within the automation tick's cadence, correctly naming it — not a generic message.
- [ ] **[Deferred]** The explainable/reversible/audited optimization advisor.

# v0.6.9.0 - Advanced Intelligence, Remote Clients, Simulation & UX Completion

The last v0.6.x milestone. Its original scope spanned several genuinely separate large investments (game-data metadata/anti-cheat need Pal/save-struct decode infrastructure that still doesn't exist anywhere in this codebase; web/mobile admin and a Remote Operations Center are new client surfaces on the scale of the existing Desktop app; simulation providers and localization are each their own substantial investment). Shipped one real, working slice.

### Idle Auto-Stop with Warning and Final Player Recheck
- [x] New `AutomationTriggerKind.IdleEmpty` on the existing automation engine — continuously-observed live state (0 online players for N minutes), deliberately excluded from the fixed-schedule `NextDueUtc` polling path used by `DailyTime`/`Interval`, evaluated instead via a new `EvaluateIdleRulesAsync` tick path.
- [x] `IdleSinceUtc` is set on first observation of 0 players while `Running`, reset to `null` the instant any player is seen online or the server isn't `Running` — persisted so a streak survives a MystTiq restart.
- [x] Reuses `ExecuteRuleAsync`/`RunLifecycleActionAsync` (including the existing warning-countdown RCON broadcast) completely unchanged for the actual stop.
- [x] **Final player recheck**: immediately before the actual stop, after any warning countdown finishes broadcasting, a live player count is fetched one more time; the stop is **aborted**, not delayed, if anyone is online — a genuinely new safety check, not decorative.
- [x] Confirmed against `docs/roadmap/PRODUCT_ROADMAP.md`'s v0.8.x section before building anything: this is a narrow, single-server, opt-in feature, distinct in scope from the future multi-tier adaptive fleet resource-policy engine planned there — building it now doesn't duplicate that later work.
- [x] Verified live: created a real `IdleEmpty` rule via the API, confirmed it round-trips with the correct threshold and stays permanently excluded from the fixed-schedule poller through creation/disable/re-enable; confirmed several real automation ticks ran cleanly with no errors while the server was stopped.
- [ ] **[Deferred]** Full end-to-end idle-to-stop verification (real idle time accruing while genuinely `Running`, the warning actually broadcasting, the recheck actually observing a real player, the stop actually executing) — Windows readiness detection requires a real PalServer process with its UDP game port independently confirmed open, which no synthetic stand-in can satisfy; needs an actual dedicated server to complete, outside this session's "never touch/never risk production" constraint. **Recommended follow-up for the first future session with a real running Palworld server available.**
- [ ] **[Deferred]** Scheduled availability/automatic start (the complementary half of "demand-aware hosting") — a real, buildable follow-up using the same composition pattern, deliberately deferred to keep this pass to one concentrated slice.

### Everything Else in the Original Scope (not attempted)
- [ ] **[Deferred]** Versioned game-data metadata generation/diffing for Pals, stats, skills, items, technologies, EXP, icons, map objects.
- [ ] **[Deferred]** Rule-based explainable anti-cheat/world-integrity scanning and review-first enforcement.
- [ ] **[Deferred]** Web/mobile administration and the Remote Operations Center.
- [ ] **[Deferred]** Simulation/mock providers for lifecycle/players/crashes/hangs/disk/ports/backup/MOD/auth failures, and deterministic regression/acceptance scenarios built on them.
- [ ] **[Deferred]** Resource-based localization and community language packs.
- [ ] **[Deferred]** Portable Windows mode.
- [ ] **[Deferred]** Dashboard customization and final UX consistency polish.

# Cross-Version Critical Scenarios

These remain permanent regression scenarios after their underlying feature exists:

1. Normal lifecycle: Start -> verify -> player -> save -> backup -> graceful restart -> reconnect -> Stop; automation must not restart after manual Stop.
2. Unexpected crash: classify -> capture -> recover -> repeated crash -> backoff -> Crash Loop.
3. Restore failure: detect/rollback -> original server starts.
4. Update failure: fail verification -> restore previous version/state.
5. Unauthorized remote action: deny through GUI/API paths.
6. External server adoption: detect existing instance -> no duplicate -> safely stop correct tree.
7. Safe guild/base mutation: stopped -> preview -> apply -> reparse -> verify unrelated data -> rollback remains available on failure.
8. Multi-server isolation: simultaneous A/B operations never cross-target.
9. Kick/ban provider fallback: REST unavailable -> RCON attempted -> honest result either way, never a silent success or an unhandled crash.
10. Idle auto-stop: server goes empty -> warning broadcasts -> a player joining mid-countdown aborts the stop, not just delays it.

# Definition of Done

A milestone is complete only when intended behavior works, unsafe states are rejected, failure paths are recoverable, authoritative state survives restart, actions are observable/auditable, and all prior critical regressions remain green. A milestone's checklist items marked **[Deferred]** are explicit, documented scope reductions — not silent gaps — each cross-referenced to the architecture doc that names the deferral and, where applicable, the future milestone expected to pick it up.
