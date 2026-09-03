# v0.4.12.0 GUI parity audit — legacy v0.2.16.4 vs Avalonia

This audit compares the final v0.2.16.4 Windows WPF surface with the cross-platform Avalonia client. A page is not considered parity-complete merely because a navigation item or heading exists; its user operations must trace through View → ViewModel → API/service → endpoint → Core/platform implementation.

| Legacy area | v0.4.12.0 status | Avalonia/headless state |
|---|---|---|
| Dashboard | Migrated / acceptance pending | Single aggregate polling, lifecycle, CPU history graph, authoritative World Pulse, full/copyable world identity, server name/description, backups, players, MOD health, live activity and combined console tail. |
| Server Setup | Migrated/Partial — parity pass 1 | Restored the legacy-style environment health checklist (Action/Component/Status/Location/Details), server operations row, operation monitor/progress, SteamCMD provisioning, and distribution update/verify wiring. The old first-time new-server defaults wizard and some dependency-specific installers remain to migrate. |
| Configuration | Migrated in this fix | Active `PalWorldSettings.ini` full OptionSettings editor plus separate MystTiq headless configuration. Timestamped rollback copy on save. |
| Live Console & RCON | Migrated / acceptance pending | Green combined console plus severity/category/search filtering, Hide routine REST, pause/clear/export and server-side RCON status/Doctor/connect/send/history. RCON is explicitly labelled legacy/deprecated and credentials stay server-side. |
| Players | Migrated / acceptance pending | Unified live/known directory, server-side save discovery, stable-ID selection, search/view/admin filters, detail/evidence, CSV export, audited notes/warnings, online-gated Kick/Ban, and visibly unavailable unsupported actions. |
| Guilds | Migrated / acceptance pending | Search/status filters, CSV export, directory/detail, ID copy, leader-player navigation, member/base evidence, and explicit mutation capability truth over authoritative decoded guild data. |
| Backups | Migrated / acceptance pending | Inventory/create/delete, confirmation-gated restore with safety backup/rollback, deep selected/all verification with persisted audit evidence, immutable retention preview/apply, and profile-aware Open Backup Root through the headless API. |
| MOD Dashboard | Partial | Inventory/health/verify state is wired; legacy deep capability/compatibility/report workflows remain. |
| MOD Library | Partial | Inventory and enable/disable controls are wired; legacy local Workshop import/details remain. |
| Manager Settings | Partial | Connection profiles and headless service configuration are wired; legacy automation/notification preferences remain. |
| Server Doctor | Partial | Headless production checks are wired; legacy full repair/action catalog remains. |
| Crash Analyzer | Not migrated | Placeholder only; legacy crash diagnostic service remains in the reference project. |
| Update Center | Migrated/Partial | PalServer SteamCMD update/validation is wired; multi-component MystTiq/UE4SS/Workshop update dashboard remains. |
| UE4SS | Partial | Runtime evidence is wired; legacy install/repair/migration actions remain. |
| World Inspector | Partial | Canonical worlds/files and IDs are wired. Legacy Overview/Players/Guilds/Bases/Saves/Statistics/Files/World Health/Integrity depth remains. |
| World Validator | Not migrated | Legacy validator/relationship checks not yet exposed through headless API. |
| World Management / Repair / Transaction Center | Not migrated | Legacy repair planning/execution/transaction journaling remains in reference source. |
| Recovery | Not migrated | Character/player/guild/base recovery workflows remain in legacy source. |
| Base Manager | Migrated read-only / acceptance pending | Search/status filters, CSV export, base ownership evidence/detail, ID copy, and explicit repair/recovery BACKEND REQUIRED surfaces. |
| Activity & Audit | Migrated/Partial | Persistent MystTiq activity/audit logs are wired; legacy historical analytics/filter depth remains. |
| Notifications | Not migrated | Legacy notification center remains in reference source. |
| Palworld Save Tools | Not migrated | Placeholder only; save inspector/import/repair tooling remains in reference source. |
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

## v0.4.12.0 focus

v0.4.12.0 restores **Backup Center parity** through the headless service. Verification reads archive contents, validates paths, calculates SHA-256 evidence, persists results atomically, and audits summaries. Retention apply consumes a single-use preview and refuses changed inventory. Restore requires explicit confirmation and preserves stopped-server, pre-restore safety backup, staging, and rollback behavior. Only a local profile may shell-open the backup root.

## v0.4.12.0 Configuration parity status

Configuration is now a focused parity area. Restored controls: Simple/Advanced view switching, search, category filter, historical QoL presets, dirty-state indicator, validation summary, reset-unsaved, local JSON import/export, load active, and rollback-backed API Save Changes. Import/export is client-side file selection only; authoritative server writes remain through the headless Palworld configuration API. Unknown/custom OptionSettings remain in the advanced collection.
