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

## v0.4.x — Windows Service Architecture, Character Migration & Diagnostic Consolidation

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

## v0.5.x — Advanced Administration, Automation & Remote Management

Competitive-feature expansion while remaining deeply Palworld-specific rather than becoming a generic multi-game hosting panel.

High-priority areas:

- **Automation / Event Engine** — schedules plus player joins/leaves, empty server, crashes, health changes, backup/update/MOD events, resource thresholds, webhook actions and approved scripts
- **Secure Remote Web Dashboard** — responsive PC/tablet/phone administration built on the headless API, after authentication/TLS hardening
- **Advanced Player Inspector / Administration** — identity, stats, inventory, equipment, Pals, technology, guild, save integrity and protected edits/repairs
- **Guild Manager** — leaders, members, bases, base Pals, storage/research where supported, leader transfer, membership repair, merge/delete-empty workflows
- **Server-wide Item / Pal Search** — locate items/Pals across characters, inventories and guild/base storage where save data supports it
- **Smart Backup & Retention Engine** — changed-world/dirty backups, pre-update/MOD/migration/repair backups, hourly/daily/weekly/monthly retention, local/network/SFTP/S3-compatible targets, integrity verification and test restore
- **Update Policies / Version Hold** — notify only, automatic, maintenance-window, manual approval, freeze/hold; optional MOD compatibility check + backup + player warning + verify + Doctor sequence
- **Users / Roles / Permissions** — Owner, Administrator, Moderator, Operator, Viewer and fine-grained control of destructive operations
- **Administrative Audit Trail** — searchable record of lifecycle, player moderation, updates, backups, migrations, repairs and automation actions
- **Discord Integration** — server/player/update/MOD/backup/Doctor alerts and later permission-controlled commands
- **Idle Shutdown / On-Demand Mode** — save/backup/graceful stop after configurable empty time; groundwork for wake-on-demand
- **Server & Player Analytics** — uptime, crashes, player counts, unique/new players, sessions, playtime and peak periods
- **Network Doctor** — listening ports, firewall, NAT/forwarding guidance, external reachability and repair recommendations integrated into Server Doctor
- **Integrated File Manager** — safe view/edit/upload/download/archive/search/config-validation with protected destructive locations
- performance profiles / resource watchdogs
- reusable server configuration presets
- authenticated webhooks / external integrations

Explicit non-goals for v0.5: multi-game hosting and commercial billing/hosting-provider features.

## v0.6.x — Multi-Server / Multi-Instance Management

Add first-class management of multiple independent Palworld instances.

- stable `ServerId` and human-readable server name
- independent worlds, configs, ports, mods, backups, automation and update policies
- create/import/remove server instances
- per-server lifecycle and Doctor state
- aggregate fleet dashboard and individual server context
- coordinated updates/restarts and maintenance windows
- per-instance permissions/audit records
- eventual support for instances on multiple machines/nodes

Architectural rule: v0.3-v0.5 schemas/APIs/automation/audit records should avoid assuming one server forever so v0.6 does not require another fundamental rewrite.

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
