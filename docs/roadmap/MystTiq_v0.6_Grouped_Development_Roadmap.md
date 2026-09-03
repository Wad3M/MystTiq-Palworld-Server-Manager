# MystTiq Palworld Server Manager
## Consolidated v0.6 Development Roadmap - Grouped Revision Plan

**Baseline:** v0.5.1.5
**Development family:** v0.6.x.x
**Source:** Consolidated review of MystTiq plus eight external Palworld server-management projects.

> **Renumbering note:** This plan was originally drafted as a `v0.5.x.x` family against a `v0.4.6.0` baseline, before the `v0.5.1.x` "Functional Visual Shell Integration" (Avalonia GUI-parity restoration) line existed. That line has since consumed `v0.5.1.0`–`v0.5.1.5` and is still the current release candidate, so this plan has been promoted wholesale to the `v0.6.x.x` family to avoid colliding with already-shipped version identifiers. Every `v0.5.x.0` milestone header below has been renumbered to `v0.6.x.0` (shifted by one MINOR version, same REVISION-slot order). Every `### Absorbs → Original v0.5.X.0...` line is left unchanged — those cite the older, pre-consolidation 27-milestone plan for historical provenance and are not live version identifiers.
>
> This also supersedes the existing `docs/roadmap/PRODUCT_ROADMAP.md` `v0.6.x — Multi-Server / Multi-Instance Management` section, which is now milestone `v0.6.8.0` below, and the terse `v0.5.x — Advanced Administration, Automation & Remote Management` wishlist in that same document, which this plan expands into `v0.6.0.0`–`v0.6.7.0` and `v0.6.9.0` in testable detail. `PRODUCT_ROADMAP.md` has been updated to point here instead of duplicating that scope.

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

# v0.6.1.0 - Guardian & Transactional Server Safety

### Goal
Make server lifecycle, backups, restores, files, archives and secrets resilient and recoverable.

### Scope
- Persistent Desired State: Running / Stopped / Maintenance.
- Evidence-based actual-state classifier.
- Process, process-tree, port, REST, RCON, GameData, log and save evidence.
- External process adoption and fresh authoritative process scans.
- Graceful shutdown escalation and process-tree termination.
- Hung detection, crash recovery, crash-loop detection and backoff.
- Diagnostic capture on suspended recovery.
- Explicit manual/graceful stop classification.
- Formal backup metadata and backup classes.
- Separate retention policies; protect manual/emergency backups.
- Native-backup configuration health.
- Save request + save/file stability verification.
- Free-space preflight, manifests, hashes and archive validation.
- Staged restore, pre-restore backup, atomic activation, startup validation and automatic rollback.
- Backup import.
- Recycle Bin / Trash for ordinary deletes.
- Central `SafeArchiveService`.
- Path traversal/link/size/count/compression protections.
- Download/update source validation and hash/signature checks.
- Protected secret storage and secret scrubbing.

### Absorbs
Original v0.5.2.0 through v0.5.4.0.

# v0.6.2.0 - Provider Framework & Configuration Intelligence

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

# v0.6.3.0 - Automation, Notifications & Secure Remote Control

### Goal
Create one persistent automation/security platform for schedules, notifications and remote/external control.

### Scope
- Trigger -> Conditions -> Actions engine.
- Persistent execution records and due-time scheduling.
- Missed/skipped/cancelled/failed/completed states.
- Retry, jitter, pending actions and operation-aware scheduling.
- Warning countdown policies and scheduled announcements.
- Event configuration snapshots and exact restoration.
- HTTP/webhook, RCON, REST/GameData/PalDefender, lifecycle, backup/update/MOD and permission-gated script actions.
- Notification routing matrix.
- Desktop, Discord admin/public, webhook and provider-ready email routing.
- Notification templates.
- Discord/external commands routed through authentication, RBAC, Operation Coordinator and audit.
- Multi-user RBAC with fine-grained capabilities.
- Per-server permissions and temporary scoped guest grants.
- Rate limiting, progressive delay, lockout and authentication-abuse auditing.
- Explicit allowed-origin/CORS policy.
- Authenticated TLS remote management and least-exposure networking.

### Absorbs
Original v0.5.7.0 through v0.5.9.0.

# v0.6.4.0 - Player Registry, Activity Intelligence & World Explorer 2

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

# v0.6.5.0 - Safe World Editing, Administration & Player Recovery

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
- Player identity mismatch detection and UID remapping wizard.
- Structural Level.sav reference changes, player-save migration and guild/reference validation.
- Integrate legacy repair tools into the same mutation framework.

### Absorbs
Original v0.5.12.0 through v0.5.14.0.

# v0.6.6.0 - MOD/UE4SS Platform, Monitoring & Analytics

### Goal
Manage extension lifecycles safely and add historical operational intelligence.

### Scope
- Mod load order, dependencies and profiles.
- Release discovery and one-click update.
- Backup, staged install, runtime verification and rollback.
- Nexus integration where appropriate.
- Latest vs tested/recommended vs installed version.
- UE4SS dedicated-server validation.
- PalDefender lifecycle integration.
- MOD runtime-state providers.
- Tiered metric retention.
- CPU, RAM, server FPS, players, provider latency, save/backup duration and storage growth.
- Honest graph gaps and restart markers.
- Alert Center.
- Disk-space prediction and days-until-full.
- Explainable/reversible/audited optimization advisor.

### Absorbs
Original v0.5.15.0 and v0.5.16.0.

# v0.6.7.0 - Transactional Updates, Discovery, Setup & Migration

### Goal
Make Palworld/MystTiq updates and server deployment/migration transactional and easy to validate.

### Scope
- Transactional Palworld update with backup, warnings, desired-state preservation, verification and rollback.
- Transactional MystTiq self-update with staging, validation, dedicated updater, locked-file retries, previous-build preservation and rollback.
- Update history/audit.
- First-run setup wizard.
- Palworld installation discovery via configured paths, registry, Steam libraries, SteamCMD, common paths, running process and profiles.
- Confidence scoring.
- Existing/SteamCMD/Steam installation providers.
- Public/local address assistant, firewall/listener checks.
- Migration wizard with source inspection, compatibility report, copy, validation, test start and source preservation.
- Doctor hardware/resource checks.

### Absorbs
Original v0.5.17.0 and v0.5.18.0.

# v0.6.8.0 - Multi-Server Fleet & Runtime Providers

### Goal
Remove single-server and single-runtime assumptions.

### Scope
- Stable server/profile IDs and isolated per-server state.
- Compound process identity.
- Fleet dashboard and independent health.
- Backup All, Doctor All and coordinated Update All.
- Scheduler jitter/staggered maintenance.
- Per-server permissions and visual identity.
- Multi-server Operation Coordinator awareness.
- Runtime providers:
  - Windows native
  - Linux native
  - official Pocketpair Docker
  - optional Windows server via Wine on Linux
- Runtime-independent Guardian, lifecycle, backup, update, Doctor, automation and operations.
- Docker-specific health/config checks.

### Absorbs
Original v0.5.19.0 and v0.5.20.0. Also supersedes the standalone "v0.6.x — Multi-Server / Multi-Instance Management" section previously in `docs/roadmap/PRODUCT_ROADMAP.md`.

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

### Absorbs
Original v0.5.21.0 through v0.5.26.0.

# Recommended Development Sequence

1. v0.6.0.0 - Architecture & Operation Platform
2. v0.6.1.0 - Guardian & Transactional Safety
3. v0.6.2.0 - Providers & Configuration
4. v0.6.3.0 - Automation, Notifications & Security
5. v0.6.4.0 - Player Intelligence & World Explorer
6. v0.6.5.0 - Safe World Editing & Recovery
7. v0.6.6.0 - MOD Platform & Analytics
8. v0.6.7.0 - Updates, Setup & Migration
9. v0.6.8.0 - Fleet & Runtime Providers
10. v0.6.9.0 - Advanced Intelligence, Remote Clients, Simulation & UX

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
