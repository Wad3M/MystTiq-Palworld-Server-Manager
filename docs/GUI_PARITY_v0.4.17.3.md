# v0.4.17.3 GUI parity audit — legacy v0.2.16.4 vs Avalonia

This audit compares the final v0.2.16.4 Windows WPF surface with the cross-platform Avalonia client. A page is not considered parity-complete merely because a navigation item or heading exists; its user operations must trace through View → ViewModel → API/service → endpoint → Core/platform implementation.

| Legacy area | v0.4.17.3 status | Avalonia/headless state |
|---|---|---|
| Dashboard | Migrated / acceptance pending | Single aggregate polling, lifecycle, CPU history graph, authoritative World Pulse, full/copyable world identity, server name/description, backups, players, MOD health, live activity and combined console tail. |
| Server Setup | Migrated/Partial — parity pass 1 | Restored the legacy-style environment health checklist (Action/Component/Status/Location/Details), server operations row, operation monitor/progress, SteamCMD provisioning, and distribution update/verify wiring. The old first-time new-server defaults wizard and some dependency-specific installers remain to migrate. |
| Configuration | Migrated in this fix | Active `PalWorldSettings.ini` full OptionSettings editor plus separate MystTiq headless configuration. Timestamped rollback copy on save. |
| Live Console & RCON | Migrated / acceptance pending | Green combined console plus severity/category/search filtering, Hide routine REST, pause/clear/export and server-side RCON status/Doctor/connect/send/history. RCON is explicitly labelled legacy/deprecated and credentials stay server-side. |
| Players | Migrated / acceptance pending | Unified live/known directory, server-side save discovery, stable-ID selection, search/view/admin filters, detail/evidence, CSV export, audited notes/warnings, online-gated Kick/Ban, and visibly unavailable unsupported actions. |
| Guilds | Migrated / acceptance pending | Search/status filters, CSV export, directory/detail, ID copy, leader-player navigation, member/base evidence, and explicit mutation capability truth over authoritative decoded guild data. |
| Backups | Migrated / acceptance pending | Inventory/create/delete, confirmation-gated restore with safety backup/rollback, deep selected/all verification with persisted audit evidence, immutable retention preview/apply, and profile-aware Open Backup Root through the headless API. |
| MOD Dashboard | Migrated / acceptance pending | Inventory, neutral-health semantics, verification, evidence, counts, and audited repair are wired. |
| MOD Library | Migrated / Partial | Inventory, selected/all state, selected delete, and validated ZIP install are wired. Workshop/legacy actions remain explicitly unavailable. |
| Manager Settings | Partial | Connection profiles and headless service configuration are wired; legacy automation/notification preferences remain. |
| Server Doctor | Partial | Headless production checks are wired; legacy full repair/action catalog remains. |
| Crash Analyzer | Migrated / acceptance pending | Bounded recent-log evidence, explicit signature grouping, durable history, stability summary, and non-destructive isolation guidance are server-authoritative. Weak proximity is never presented as proof. |
| Update Center | Migrated/Partial | PalServer SteamCMD update/validation is wired; multi-component MystTiq/UE4SS/Workshop update dashboard remains. |
| UE4SS | Partial | Runtime evidence is wired; legacy install/repair/migration actions remain. |
| World Inspector | Migrated / acceptance pending | Overview, Players, Guilds, Bases, Saves, Statistics, Files, World Explorer, World Health, and Integrity use canonical remote-safe headless evidence. |
| World Validator | Migrated / structural parity | Server-authoritative active-world validation, findings, and local report export are wired. Semantic binary relationship validation remains codec-dependent. |
| World Management / Repair / Transaction Center | Migrated / partial | Archive Analyze/Review/Apply uses stopped-server enforcement, fresh backup, isolated staging, atomic swap, post-validation, rollback and durable server-side journals. |
| Recovery | Migrated / partial | Full-world import and canonical player-save recovery are transactional. Guild/base binary repair remains visibly BACKEND REQUIRED pending a safe codec. |
| Base Manager | Migrated read-only / acceptance pending | Search/status filters, CSV export, base ownership evidence/detail, ID copy, and explicit repair/recovery BACKEND REQUIRED surfaces. |
| Activity & Audit | Migrated / acceptance pending | Persistent evidence plus search, severity/category filters and local visible-view export are wired. Persistent clear is intentionally policy-locked. |
| Notifications | Migrated / acceptance pending | Persistent bounded store, bell badge, search/severity, read/unread, pin/unpin, dismiss, mark-all, semantic self-test and export are wired. |
| Palworld Save Tools | Migrated / read-only parity | Server-side Python, legacy converter, PlM converter, Oodle and active-save diagnostics plus bounded save inventory and self-tests. Mutation remains in the protected World Transaction workflow. |
| Workspace | Migrated/Partial | Managed paths are shown; legacy browse/validate/open-folder actions remain to migrate. |
| Diagnostics Center | Migrated/Partial | Network/firewall/listener diagnostics are wired; legacy broader diagnostics catalog remains. |

## Legacy window-set iteration order

The v0.2.16.4 source contains the following user-visible window/page set and nested World Inspector tabs. We will review these in order against Avalonia, preserving behavior through the headless architecture rather than copying WPF event handlers directly:

1. Dashboard → Server Setup → Configuration → Console → Players → Guilds → Backups.
2. MOD Dashboard → MOD Library → Settings → Server Doctor → Crash Analyzer → Update Center → UE4SS.
3. World Inspector: Overview, Players, Guilds, Bases, Saves, Statistics, Files, World Explorer, World Health, Integrity, World Validator, World Management, Repair Center, Transaction Center.
4. Recovery → Player Recovery → Guild & Base Recovery → Base Manager → Activity & Audit → Notifications → Palworld Save Tools → Workspace → Diagnostics Center.

Each pass must update this matrix with **Migrated**, **Partial**, **Not migrated**, or **Intentionally retired**, and each migrated operation needs a behavioral route test.

## Parity rule going forward

Every future page migration must include behavioral tests for its real route, not only XAML labels. The remaining partial/not-migrated rows above are explicit backlog and may not be declared parity-complete until their old user-visible operations have a cross-platform headless equivalent or are intentionally retired with documentation.

## v0.4.17.3 focus

v0.4.17.3 restores Crash Analyzer and Palworld Save Tools parity through on-demand authenticated routes. The desktop never scans remote logs or saves. Crash evidence is bounded and persistent, while save inspection and dependency self-tests are read-only; all save mutation stays behind the established transaction safety contract.

## v0.4.16.0 Configuration parity status

Configuration is now a focused parity area. Restored controls: Simple/Advanced view switching, search, category filter, historical QoL presets, dirty-state indicator, validation summary, reset-unsaved, local JSON import/export, load active, and rollback-backed API Save Changes. Import/export is client-side file selection only; authoritative server writes remain through the headless Palworld configuration API. Unknown/custom OptionSettings remain in the advanced collection.
