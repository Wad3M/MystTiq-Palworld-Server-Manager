# v0.5.3.0 screen-by-screen comparison — v0.2.16.4 to Avalonia

This pass reads each v0.2.16.4 screen from the supplied `MainWindow.xaml` and its matching partial code-behind, then compares it with the rendered v0.5.3.0 Avalonia destination. Appearance alone is not proof of parity: state-changing controls must retain the View → ViewModel → API client → route → headless service chain.

## Shared layout measurements

- Legacy standard single-line fields use 30–34 px height; v0.5.3.0 now uses a global 34 px minimum with 9×4 padding for both `TextBox` and `ComboBox`.
- Legacy compact numeric fields use explicit widths of 55–72 px. Avalonia keeps explicit compact widths where the value is intrinsically short and lets identity/path fields stretch with the card.
- Legacy search fields are commonly 170–190 px when placed in a command row. Avalonia uses proportional grid columns so they can grow without leaving their card.
- Legacy UE4SS uses a 190 px source/fork selector and a release selector with a 420 px minimum width; v0.5.3.0 restores the source/fork decision while leaving the release catalog capability explicitly backend-required.
- Legacy multiline fields range from 65 px notes to 360 px diagnostic output. Avalonia retains page-specific explicit heights and scroll behavior; the global 34 px rule is only a minimum.
- All Avalonia inputs, lists and cards remain clipped to their owning surface.

## Screen order and findings

| # | v0.2.16.4 screen | v0.5.3.0 destination | Comparison result / remaining gap |
|---:|---|---|---|
| 1 | Dashboard | Home → Dashboard | Dense server/world/backup/health/player/MOD/resource/activity/log surfaces and lifecycle actions are restored over the single aggregate poll. |
| 2 | Server Setup | Server → Server Setup | Component/ready/attention summaries, first-run defaults, authoritative environment checklist, row actions, server operations and operation monitor are restored. Default creation is confirmation-gated, server-side, validated, and refuses overwrite. |
| 3 | Configuration | Server → Configuration | Lifecycle/status duplication is removed. Identity, separate Simple sliders/markers, Advanced default/active table, search, filters, presets, validation, import/export and rollback-backed save are restored. |
| 4 | Console | Server → Console | CPU/RAM/thread panels are removed. Green timestamp-first combined logs, search/category/severity, pause, clear, export and headless RCON are restored. |
| 5 | Players | World → Players | Directory, details, filtering, notes/warnings, export and supported administration are restored. |
| 6 | Guilds | World → Guilds | Directory, membership/base evidence, filters, export and player navigation are restored. |
| 7 | Backups | Backups | Create, Verify Selected/All, Restore, Delete, Refresh and Open Root are restored to the top row; inventory summaries, retention preview/apply and guarded restore remain live. |
| 8 | MOD Dashboard | Mods → MOD Dashboard | Health/evidence counts and verification remain distinct from runtime management. |
| 9 | MOD Library | Mods → MOD Library | Installed MOD inventory and supported mutations are restored; Workshop automation remains capability-gated. |
| 10 | Settings | System → Settings | Connection profiles and headless configuration are restored. The top `+` now opens a blank, unsaved server profile editor. |
| 11 | Server Doctor | Tools → Server Doctor | Production checks are restored; the larger legacy action/repair catalog remains partial. |
| 12 | Crash Analyzer | Tools → Crash Analyzer | Bounded evidence and history are restored without claiming weak correlations as causes. |
| 13 | Update Center | Tools → Update Center | Platform, SteamCMD and PalServer summary cards plus PalServer update/validation are live; multi-component UE4SS/Workshop update orchestration remains partial. |
| 14 | UE4SS | Mods → UE4SS | Generic Installed MODs inventory removed. Installed runtime version, health, resolver evidence and fork choice are shown. GitHub release catalog/install/rollback remains explicitly BACKEND REQUIRED. |
| 15 | World Inspector / Overview | World → Inspector | Canonical world identity and overview are restored. |
| 16 | Inspector / Players | World → Inspector sections | Read-only decoded player evidence is restored; operational player work remains on the Players page. |
| 17 | Inspector / Guilds | World → Inspector sections | Decoded guild evidence is restored. |
| 18 | Inspector / Bases | World → Inspector sections | Decoded base-reference evidence is restored. |
| 19 | Inspector / Saves | World → Inspector sections | Canonical save inventory is restored. |
| 20 | Inspector / Statistics | World → Inspector sections | Server-authoritative counts and integrity metadata are restored. |
| 21 | Inspector / Files | World → Inspector sections | Remote-safe file evidence is restored without desktop filesystem access. |
| 22 | Inspector / World Explorer | World → Inspector sections | Canonical world/save discovery is restored and excludes backup-like snapshots. |
| 23 | Inspector / World Health | World → Inspector sections | Health evidence is restored. |
| 24 | Inspector / Integrity / Validator | Inspector → Validator & Recovery | Validation and local report export are restored; deep binary semantics remain codec-dependent. |
| 25 | World Management / Repair / Transactions | Inspector → Validator & Recovery | Preview, backup, staged transaction, validation, rollback and audit are restored. |
| 26 | Recovery / Player Recovery | Inspector → Validator & Recovery | Whole-world and canonical player-save recovery are guarded; unsafe guild/base mutation remains unavailable. |
| 27 | Base Manager | World → Bases | Read-only ownership evidence, filters and export are restored; repair remains unavailable pending safe backend support. |
| 28 | Activity & Audit | System → Activity & Audit | Persistent evidence, filters and export are restored; audit deletion remains policy-locked. |
| 29 | Notifications | System → Notifications | Moved from Home to System. Persistent read/pin/dismiss/self-test/export behavior remains API-backed. |
| 30 | Palworld Save Tools | Tools → Save Tools | Server-side Python/converter/Oodle diagnostics and inventory are restored read-only. |
| 31 | Workspace | Server → Workspace | Deployment mode, workspace health, server discovery, executable/application/server/runtime/download/export/log locations and appropriate local-only Browse/Open actions are restored with remote profile gating. |
| 32 | Diagnostics Center | Tools → Diagnostics | Network/firewall/listener diagnostics and redacted support output are restored. |

## v0.5 shell-only comparison

The server-session tabs and add-tab control come from the approved v0.5.0.18 shell rather than v0.2.16.4. v0.5.3.0 binds the tabs to real persisted connection profiles. The `+` control creates an unsaved editor state and opens System → Settings; the profile is not persisted until Save Profile is invoked.
