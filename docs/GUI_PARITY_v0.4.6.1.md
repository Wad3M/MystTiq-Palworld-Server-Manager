# v0.4.6.1 GUI parity audit — legacy v0.2.16.4 vs Avalonia

This audit compares the final v0.2.16.4 Windows WPF surface with the cross-platform Avalonia client. A page is not considered parity-complete merely because a navigation item or heading exists; its user operations must trace through View → ViewModel → API/service → endpoint → Core/platform implementation.

| Legacy area | v0.4.6.1 status | Avalonia/headless state |
|---|---|---|
| Dashboard | Migrated | Aggregate status polling, lifecycle, resources, players, backup/world/MOD summaries. |
| Server Setup | Partial | Distribution/install/update evidence is wired; the legacy environment checklist and new-server wizard still need deeper migration. |
| Configuration | Migrated in this fix | Active `PalWorldSettings.ini` full OptionSettings editor plus separate MystTiq headless configuration. Timestamped rollback copy on save. |
| Live Console & RCON | Partial | Persistent PalServer stdout/stderr and log tail are wired. Full legacy RCON command library/history/Doctor controls remain to migrate. |
| Players | Partial | Live REST player inventory plus kick/ban and capability-gated context actions. Legacy history/recovery/toolkit depth remains to migrate. |
| Guilds | Partial | Decoded membership evidence is shown; advanced guild administration/repair is not yet migrated. |
| Backups | Migrated | Inventory/create/delete/restore through headless API. |
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
| Base Manager | Partial | Base evidence is shown; legacy ownership/recovery management remains. |
| Activity & Audit | Migrated/Partial | Persistent MystTiq activity/audit logs are wired; legacy historical analytics/filter depth remains. |
| Notifications | Not migrated | Legacy notification center remains in reference source. |
| Palworld Save Tools | Not migrated | Placeholder only; save inspector/import/repair tooling remains in reference source. |
| Workspace | Migrated/Partial | Managed paths are shown; legacy browse/validate/open-folder actions remain to migrate. |
| Diagnostics Center | Migrated/Partial | Network/firewall/listener diagnostics are wired; legacy broader diagnostics catalog remains. |

## Parity rule going forward

Every future page migration must include behavioral tests for its real route, not only XAML labels. The remaining partial/not-migrated rows above are explicit backlog and may not be declared parity-complete until their old user-visible operations have a cross-platform headless equivalent or are intentionally retired with documentation.

## v0.4.6.1 prerequisite note

Page parity is not considered runtime-valid unless the desktop has a compatible management session. v0.4.6.1 therefore makes connection/session establishment and Start Server acceptance a prerequisite before additional legacy page areas can be promoted as migrated.
