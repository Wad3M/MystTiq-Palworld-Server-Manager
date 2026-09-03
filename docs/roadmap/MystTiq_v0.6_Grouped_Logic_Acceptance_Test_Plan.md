# MystTiq Palworld Server Manager
## v0.6 Grouped Logic & Acceptance Test Plan

**Purpose:** Prove that each grouped `v0.6.x.0` milestone achieves its intended behavior before advancing.

> Renumbered from the originally-drafted `v0.5.x.0` family (baseline `v0.4.6.0`) to `v0.6.x.0` to avoid colliding with the already-shipped `v0.5.1.x` GUI-parity/shell-integration line. See `MystTiq_v0.6_Grouped_Development_Roadmap.md` for the renumbering rationale. Only the milestone headers changed; every checklist item is unchanged from the original plan.

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
- [ ] v0.5.1.5 critical logic and runtime smoke still pass.
- [ ] Existing configuration/state loads without loss.
- [ ] Canonical Server/Profile ID exists and persists.
- [ ] Operation, provider/capability, health, event and transaction contracts exist in Core/service boundaries.
- [ ] Start/update/backup/restore long-running actions can create unique Operation IDs.
- [ ] Operation record contains server/profile, type, owner/source, phase, status and timestamps.
- [ ] Conflicting operations on the same resource cannot execute simultaneously.
- [ ] Non-conflicting allowed operations can execute concurrently.
- [ ] Blocked operation reports what resource/dependency it is waiting for.
- [ ] Queue ordering is deterministic.
- [ ] Priority behavior works where allowed.
- [ ] Cancelling a queued operation prevents it from starting.
- [ ] Cancelling a running cancellable operation releases resources safely.
- [ ] Failed/interrupted operations release locks.
- [ ] Service restart does not leave stale locks.
- [ ] Interrupted persisted operations are classified appropriately after restart.
- [ ] Operations Center displays phase/progress/logs/history.
- [ ] SteamCMD output maps to meaningful phases when applicable.
- [ ] Legacy parity/migration inventory and v0.6 architecture documentation are present.

# v0.6.1.0 - Guardian & Transactional Server Safety

### Guardian / Lifecycle
- [ ] Process + healthy providers = Running Healthy.
- [ ] Process + partial provider failure = Running Degraded.
- [ ] No process + Desired Running = Crashed.
- [ ] No process + Desired Stopped = Stopped/Offline, not Crashed.
- [ ] Maintenance prevents unintended auto-restart.
- [ ] Externally started Palworld process is adopted correctly.
- [ ] Wrong Palworld instance is not adopted.
- [ ] Fresh scan prevents duplicate Start when cache is stale.
- [ ] Launcher exit with shipping process alive still reports running.
- [ ] Graceful shutdown is attempted before force termination.
- [ ] Only intended process tree is terminated.
- [ ] Unexpected crash restarts when Desired Running.
- [ ] Manual stop does not auto-restart.
- [ ] Repeated crashes back off and eventually enter Crash Loop.
- [ ] Crash-loop suspension captures diagnostics.
- [ ] Temporary provider timeout does not falsely classify Hung.
- [ ] Sustained non-responsiveness reaches Hung according to policy.

### Backup
- [ ] Manual, Automatic, PreUpdate, PreRestore, PreRepair and Emergency classes are stored correctly.
- [ ] Manual/emergency backups are not pruned by automatic retention.
- [ ] Game save is requested before backup when supported.
- [ ] Save/file stability is verified rather than using only a fixed sleep.
- [ ] Failed save request follows configured retry/degraded/abort policy.
- [ ] Free-space preflight blocks unsafe backup.
- [ ] Manifest/hash metadata is created.
- [ ] Valid archive verifies.
- [ ] Corrupt archive fails.
- [ ] Backup import validates before registration.

### Restore / Rollback
- [ ] Unsafe restore while running is rejected.
- [ ] Pre-restore backup is automatic.
- [ ] Archive validates before extraction.
- [ ] Restore extracts to staging.
- [ ] Staged world validates before activation.
- [ ] Activation is atomic/safely swapped.
- [ ] Desired state controls whether server restarts.
- [ ] Post-restore health/world identity verifies.
- [ ] Corrupt/failed restore rolls back.
- [ ] Failed post-restore startup rolls back.
- [ ] Partial live files are never left behind.

### File / Archive / Secret Safety
- [ ] Ordinary delete goes to Recycle Bin/Trash and can be recovered.
- [ ] `../` traversal archive entry is rejected.
- [ ] Absolute path is rejected.
- [ ] Escaping symlink/hardlink is rejected.
- [ ] File-count, expanded-size and compression-ratio limits work.
- [ ] Nothing writes outside destination.
- [ ] SafeArchiveService is used by all in-scope archive consumers.
- [ ] Unexpected update/repository source is rejected.
- [ ] Hash/signature mismatch blocks install where supported.
- [ ] Admin/RCON/API/webhook secrets are absent from normal logs.
- [ ] Diagnostic/export output scrubs secrets.
- [ ] Protected secret storage has correct platform access restrictions.

# v0.6.2.0 - Provider Framework & Configuration Intelligence

### Providers
- [ ] Each provider advertises only supported capabilities.
- [ ] REST failure can fall back to another capable player-presence provider.
- [ ] Degraded provider remains usable for unaffected capabilities.
- [ ] Misconfigured credentials produce Misconfigured.
- [ ] Unsupported version/capability is explicit.
- [ ] Provider selection is capability-based, not UI hard-coding.
- [ ] MOD runtime data can be sourced from authoritative MOD provider.
- [ ] API/command explorer shows status/latency and cannot bypass auth/RBAC.
- [ ] Dangerous command requires permission.

### Configuration
- [ ] Supported settings have schema type/default/help metadata.
- [ ] Numeric min/max is enforced.
- [ ] Enum values are constrained.
- [ ] Deprecated/reserved settings warn.
- [ ] Restart-required setting is marked.
- [ ] Cross-setting dependencies validate.
- [ ] Port conflicts are detected.
- [ ] REST/API enabled without required credentials is flagged.
- [ ] Validation occurs before write.
- [ ] Changing one managed setting does not unnecessarily rewrite unrelated values.
- [ ] Preset applies expected values.
- [ ] Import validates before apply.
- [ ] Export excludes secrets.
- [ ] Portable profile import identifies secrets that must be re-entered.

# v0.6.3.0 - Automation, Notifications & Secure Remote Control

### Scheduler / Automation
- [ ] Due action executes when Due <= Now even if exact second was missed.
- [ ] Completed execution is not duplicated.
- [ ] Missed/skipped/cancelled/failed/completed states persist.
- [ ] Retry policy works.
- [ ] Jitter remains in configured bounds.
- [ ] Service restart preserves schedules and pending actions.
- [ ] Conditions block actions when false.
- [ ] RCON, lifecycle, backup and HTTP/webhook actions work through normal services.
- [ ] Permission-gated script action cannot execute without permission.
- [ ] Event snapshot stores exact pre-event configuration.
- [ ] Event completion restores exact prior values, not defaults.
- [ ] Service restart during event preserves rollback snapshot.
- [ ] Offline/pending player action executes when trigger becomes true.

### Notifications / External Control
- [ ] Routing matrix sends each event only to configured destinations.
- [ ] Template variables resolve correctly.
- [ ] Notification-provider outage does not block server operation.
- [ ] Authorized external command creates a normal coordinated operation.
- [ ] Unauthorized external command is rejected and audited.
- [ ] External identity/source is captured in audit.

### RBAC / Remote Security
- [ ] Read-only account can view but cannot mutate.
- [ ] Fine-grained permissions distinguish kick vs ban, backup vs restore, world read vs edit.
- [ ] Per-server permissions isolate profiles.
- [ ] Scoped guest grant exposes only configured server/capabilities.
- [ ] Expired/revoked grant fails.
- [ ] API enforces permissions even if a client exposes a hidden control.
- [ ] Repeated failed authentication triggers throttling/lockout.
- [ ] Lockout expires correctly.
- [ ] Abuse events are audited.
- [ ] Unauthorized browser origin is rejected.
- [ ] Remote management requires configured authenticated TLS.
- [ ] Palworld management interfaces remain least-exposed where practical.

# v0.6.4.0 - Player Registry, Activity Intelligence & World Explorer 2

### Player Registry / Presence
- [ ] New player creates one persistent record.
- [ ] Returning player updates same identity.
- [ ] Steam ID and Player UID map correctly.
- [ ] First Seen is immutable; Last Seen updates.
- [ ] Session count increments once per actual session.
- [ ] Playtime accumulates correctly.
- [ ] Notes/watchlist/trusted/problem flags persist and respect RBAC.
- [ ] REST + GameData agreement produces high-confidence presence.
- [ ] Fallback log evidence works when APIs unavailable.
- [ ] Leave evidence clears stale presence.
- [ ] Conflicting/stale evidence is represented honestly.
- [ ] Searchable chat/activity history returns expected records.
- [ ] Queued offline moderation executes when applicable.
- [ ] Activity heatmap and peak-time aggregation match source data.
- [ ] Maintenance-window recommendation uses actual low-activity period.

### World Explorer
- [ ] Large map loads without blocking UI.
- [ ] Pan/zoom/fullscreen work.
- [ ] Marker clustering behaves correctly across zoom levels.
- [ ] Layer toggles work.
- [ ] Player/base positions match decoded/provider coordinates.
- [ ] Base radius scale is correct.
- [ ] Guild/base click opens correct inspector.
- [ ] Abandoned-base detection respects inactivity threshold and does not flag active guilds.
- [ ] Global item search finds supported containers.
- [ ] Storage/container inspector targets correct container.

# v0.6.5.0 - Safe World Editing, Administration & Player Recovery

### Mutation Engine
- [ ] Mutation while server running is rejected.
- [ ] Missing world-edit permission is rejected.
- [ ] Fresh snapshot and verified backup are required.
- [ ] Replace changes only intended bytes.
- [ ] Insert/delete correctly updates parent lengths/counts.
- [ ] Overlap, conflict and out-of-bounds edits are rejected.
- [ ] Unknown/unrelated bytes remain unchanged where possible.
- [ ] Modified buffer strictly reparses.
- [ ] Structural/semantic/invariant failures reject commit.
- [ ] Atomic replacement occurs only after validation.
- [ ] Post-write verification failure rolls back.
- [ ] Audit contains requested and actual changes.

### Administration
- [ ] Player inventory/progression edit targets intended player and enforces bounds.
- [ ] Pal nickname/level/skills/souls/talents/condenser/gender/work suitability edits validate.
- [ ] Heal/revive creates valid state.
- [ ] Clone creates distinct valid identity.
- [ ] Delete removes intended Pal only.
- [ ] Container resize preserves items; unsafe shrink is blocked/policy-gated.
- [ ] Guild/base storage edit targets correct container.
- [ ] Bulk Pal operation previews targets and can rollback.
- [ ] Teleport/summon selects a capable provider or reports unsupported.
- [ ] Kit/starter-kit operation is tested if shipped.

### Recovery
- [ ] Old and new player identities are identified.
- [ ] Wizard previews UID/reference changes.
- [ ] Relevant Level.sav references are found structurally.
- [ ] Unrelated UUIDs are not replaced.
- [ ] Player save migration/rename is correct.
- [ ] Guild membership and ownership remain valid.
- [ ] Strict validation succeeds after recovery.
- [ ] Failed recovery rolls back and is audited.

# v0.6.6.0 - MOD/UE4SS Platform, Monitoring & Analytics

### MOD Lifecycle
- [ ] Installed/latest/recommended versions are independently represented.
- [ ] Missing/circular dependencies and invalid load order are detected.
- [ ] Incompatible combinations warn/block according to policy.
- [ ] Pre-change backup occurs.
- [ ] SafeArchiveService is used.
- [ ] Installation stages before activation.
- [ ] Runtime verification confirms load.
- [ ] Failed verification rolls back.
- [ ] Desired server state is preserved.
- [ ] Mod profiles save/switch correctly.
- [ ] Disabled/unverified neutral MOD state does not falsely degrade Overall Health.
- [ ] Nexus integration works if shipped.
- [ ] UE4SS dedicated-server validation works.
- [ ] PalDefender lifecycle and provider health work if shipped.

### Monitoring / Analytics
- [ ] Raw and rollup retention tiers aggregate correctly.
- [ ] CPU, RAM, player count, provider latency, save duration, backup duration and storage growth record correctly.
- [ ] Monitoring outage renders a gap, not fabricated continuity.
- [ ] Restart markers are recorded.
- [ ] Retention deletes only expired data.
- [ ] Low-disk and forecast alerts trigger/clear correctly.
- [ ] Duplicate alert spam is suppressed.
- [ ] Advisor recommendation is explained.
- [ ] Applying advisor action is explicit, reversible and audited.

# v0.6.7.0 - Transactional Updates, Discovery, Setup & Migration

### Updates
- [ ] Palworld update availability/current version are recorded.
- [ ] Backup and warnings occur before update.
- [ ] Desired state is preserved.
- [ ] New version installs and health verifies before commit.
- [ ] Failed update rolls back previous version and state.
- [ ] MystTiq update validates package before exit.
- [ ] Dedicated updater waits for processes/services to exit.
- [ ] Locked-file retry works.
- [ ] Previous MystTiq build is retained until verification.
- [ ] Failed MystTiq startup can revert.
- [ ] Config/secrets survive update.

### Discovery / Setup
- [ ] Configured path, registry, Steam libraryfolders.vdf, SteamCMD and running-process discovery work.
- [ ] Duplicate candidates merge and confidence ranks valid install highest.
- [ ] Existing and SteamCMD installation flows work.
- [ ] Steam-client installation flow works if shipped.
- [ ] Invalid installation is rejected.
- [ ] Public/local address assistant reports expected addresses.
- [ ] Firewall/listener diagnostics run after setup.
- [ ] Hardware/resource Doctor requirements report correctly.

### Migration
- [ ] Source save/config/mods are detected.
- [ ] Destination compatibility report is generated.
- [ ] Source remains untouched until destination validates.
- [ ] Destination test start and health verification work.
- [ ] Failed migration leaves source intact.
- [ ] Successful migration clearly identifies new active profile.

# v0.6.8.0 - Multi-Server Fleet & Runtime Providers

### Fleet Isolation
- [ ] Two servers run simultaneously with independent ports/state.
- [ ] Start/stop/restart/backup/Doctor/update on A does not affect B.
- [ ] Guardian recovery targets only affected server.
- [ ] Compound process identity distinguishes same executable.
- [ ] Fleet dashboard shows independent health.
- [ ] Per-server RBAC works.
- [ ] Backup All targets every intended profile once.
- [ ] Doctor All reports each profile independently.
- [ ] Update All coordinates concurrency safely.
- [ ] Visual server identity remains consistent.

### Runtime Providers
- [ ] Windows native lifecycle/listener/backup/update works.
- [ ] Linux native lifecycle/process tree/permissions/backup/update works.
- [ ] Docker container starts with persistent save storage and correct port exposure.
- [ ] Container replacement preserves save.
- [ ] Docker update preserves desired state and can rollback.
- [ ] Wine provider lifecycle/paths/prefix work if included.
- [ ] Guardian and Operations Center use runtime abstractions rather than OS-specific GUI logic.

# v0.6.9.0 - Advanced Intelligence, Remote Clients, Simulation & UX Completion

### Metadata / Compatibility
- [ ] Metadata generation is deterministic.
- [ ] Added/removed/renamed game data is reported.
- [ ] Generated IDs are unique and references resolve.
- [ ] Parser fixtures for supported save versions pass.
- [ ] Unknown/new fields do not unnecessarily break parsing.
- [ ] Log/config compatibility tests detect format/schema changes.

### Integrity / Anti-Cheat
- [ ] Known impossible inventory/Pal/progression fixtures trigger expected rules.
- [ ] Valid high-end fixtures do not false-positive.
- [ ] Findings identify evidence/source and reason.
- [ ] Admin can dismiss/mark false positive.
- [ ] Detection alone never destroys player data.
- [ ] Enforcement requires explicit authorized action.

### Demand-Aware Runtime
- [ ] Empty server starts idle timer.
- [ ] Player return resets/cancels idle shutdown.
- [ ] Final player recheck occurs after warning.
- [ ] Still-empty server stops.
- [ ] Active operation/event/maintenance blocks or modifies policy as designed.
- [ ] Scheduled automatic start works.
- [ ] Runtime policy survives restart as designed.

### Web/Mobile
- [ ] Web/mobile loads permitted server data.
- [ ] Mobile layout is usable.
- [ ] Server-side RBAC matches desktop/API behavior.
- [ ] Remote lifecycle action creates normal Operation.
- [ ] Remote disconnect does not unexpectedly cancel server operation.
- [ ] Map/player/chat/backup/alert views match authoritative service data.
- [ ] No management business logic exists only in web client.

### Simulation / Failure Injection
- [ ] Simulate stopped/starting/healthy/degraded/hung/crashed/crash-loop states.
- [ ] Simulate player join/leave.
- [ ] Simulate low disk, port conflict, backup failure, corrupt restore, auth failure, MOD failure and update rollback.
- [ ] Same scenario produces deterministic result.
- [ ] Regression harness can run unattended.
- [ ] GUI acceptance can bind to simulated service.
- [ ] Simulation cannot control production server accidentally.

### Localization / Portable / UX
- [ ] English resource catalog complete.
- [ ] Secondary catalog key parity passes.
- [ ] Missing keys are detected.
- [ ] Community language packs contain data only and compatibility is validated.
- [ ] Portable and installed modes use isolated intended paths.
- [ ] Portable secret handling remains protected as platform permits.
- [ ] Dashboard customization persists if shipped.
- [ ] Operations, Guardian, Backup/Restore, World Edit, Fleet and Settings UX remain consistent.

# Cross-Version Critical Scenarios

These remain permanent regression scenarios after their underlying feature exists:

1. Normal lifecycle: Start -> verify -> player -> save -> backup -> graceful restart -> reconnect -> Stop; Guardian must not restart after manual Stop.
2. Unexpected crash: classify -> capture -> recover -> repeated crash -> backoff -> Crash Loop.
3. Restore failure: detect/rollback -> original server starts.
4. Update failure: fail verification -> restore previous version/state.
5. Unauthorized remote action: deny through GUI/web/API/Discord paths.
6. External server adoption: detect existing instance -> no duplicate -> safely stop correct tree.
7. Safe world edit: stopped -> preview/EditPlan -> apply -> reparse -> verify unrelated data -> start -> rollback remains available.
8. Multi-server isolation: simultaneous A/B operations never cross-target.

# Definition of Done

A milestone is complete only when intended behavior works, unsafe states are rejected, failure paths are recoverable, authoritative state survives restart, actions are observable/auditable, and all prior critical regressions remain green.
