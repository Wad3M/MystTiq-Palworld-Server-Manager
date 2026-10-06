<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# Product roadmap

Updated 2026-10-06. **Current version: v1.0.6.0. Accepted baseline: v1.0.0.0. Next milestone: none scheduled yet (to agree with the owner).**

This is the active plan. Completed work and the per-version detail behind every item below are in
[the history file](../history/PRODUCT_ROADMAP_through_v1.0.1.0.md), the [changelog](../../CHANGELOG.md) and the
[release notes](../../release-notes/). Earlier history: [through v0.8.25.0](../history/PRODUCT_ROADMAP_through_v0.8.25.0.md).

## How this roadmap works

- Every open item has an **ID**, a **status**, **acceptance** (what must be observable) and **evidence** (what must be recorded).
- **Statuses:** `Planned` · `In progress` · `Built` (code and harness checks pass; live proof still owed) · `Done` (all evidence recorded) · `Blocked` (names the owner's decision or resource).
- An item is `Done` only when its evidence is recorded. Harness-only passes stay `Built`.
- Items that slip move to the next milestone with a note. They are not dropped silently.
- Each milestone has an **exit gate**: every item is `Done`, or it is explicitly moved with a reason.
- ID prefixes: `R` reliability, `P` packaging and deployment, `W` browser view, `X` Xbox players, `S` save edits, `E` evidence carried from v1.0, `D` owner decisions, `B` backlog.

## Owner decisions (resolved 2026-10-05)

| ID | Question | Decision |
| --- | --- | --- |
| D-1 | Browser admin UI | **Read-only browser view.** No write routes in the browser. |
| D-2 | Save editing scope | **Guarded edits only.** Each edit takes an automatic backup first and passes a restore check before it counts as done. |
| D-3 | Priority of the next milestone | **Unattended reliability first** (v1.0.2.0). |
| D-4 | Platforms in the next 12 months | Windows desktop (primary), Linux headless service, Docker container, Xbox player support. |
| D-5 | Xbox test account | The owner's own Xbox account is available for live discovery. |
| D-6 | Docker test host | Docker Desktop on the owner's Windows machine, used for local container tests. |
| D-7 | First save edit | Remove an item, then add an item, then add and remove a Pal. |
| D-8 | MOD browser repositories (2026-10-06) | The owner's Downloads folder or other folders, Nexus Mods (the owner's account is free: Mod Manager Download links), Thunderstore, CurseForge, GitHub releases. |

## v1.0.2.0 — Unattended reliability

Exit gate: R-1 to R-3 are `Done` on the supported matrix (Windows and the Linux VM), and E-1 is unaffected.

Released 2026-10-05 as v1.0.2.0 with R-1 `Done`. R-2 and R-3 are `Built` and carried into the v1.0.3.0 exit gate until their live evidence is recorded (an owner-configured Discord and email channel; the Linux VM reachable).

| ID | Item | Acceptance | Evidence required | Status |
| --- | --- | --- | --- | --- |
| R-1 | Frozen-server watchdog | A server that is running but stops answering its health probe is restarted within a set time. The restart is logged and raises an alert. A healthy server is never restarted. | Live: freeze the clone (suspend the process), see the restart, the log line and the alert. Harness: healthy server untouched. Recorded 2026-10-05 (v1.0.2.0): clone frozen, restarted 84 s later, alert, Activity entry and service log line; harness covers healthy, REST off and give-up. | Done |
| R-2 | Alert delivery proof | Each send is recorded with its result. A channel with no successful delivery in a set window is flagged on the Dashboard and in Alert Center. | Harness for records and flags. Live: one real send per channel (Discord, email) confirmed arrived. Harness recorded (v1.0.2.0). Live owed: no Discord or email channel is set up yet; owner to configure, then Send test notification. Recorded (v1.0.6.0, 2026-10-06): a real webhook send through an isolated service reached an HTTP receiver and was recorded Delivered; a send to a closed port was recorded Failing with the reason. Discord and email (the owner's accounts) are still owed. | Built |
| R-3 | Linux headless service under systemd | The headless service runs without a desktop, starts at boot, restarts on failure, and stops cleanly. | Live on the Linux VM, clone server only: reboot, crash, stop. Production unit untouched. `scripts/Test-v1.0.2.0-LinuxSystemd.ps1` built (v1.0.2.0); the VM did not answer on 2026-10-05, so the live run is owed. Recorded (v1.0.6.0, 2026-10-06), VM on, no passwordless sudo, the production unit only read: the production unit (MystTiq's own unit text) started at boot, 11 s after the VM booted, and started PalServer; the v1.0.6.0 build as a per-user unit (`Test-v1.0.6.0-LinuxUserSystemd.ps1`, 8/8) ran without a desktop, was restarted by systemd after SIGKILL and stopped cleanly with its server; the production unit was unchanged. | Done |

## v1.0.3.0 — Packaging and read-only access

Exit gate: P-1 `Done`, W-1 `Done`, and X-1 has a recorded discovery result (it may be `Blocked` if Xbox data proves unavailable, with the reason written down).

Released 2026-10-05 as v1.0.3.0. W-1 is `Built` (the remote TLS session is owed) and X-1 is `Blocked` on the owner's Xbox session; both are carried into the v1.0.4.0 exit gate.

| ID | Item | Acceptance | Evidence required | Status |
| --- | --- | --- | --- | --- |
| P-1 | Docker image for the headless service | A Linux container image runs the headless service with a mounted data folder. It starts, serves the local API and runs a clone server. | Built and run on Docker Desktop with clone data. Image tagged with the MystTiq version. Recorded 2026-10-05 (v1.0.3.0) on Docker Desktop: the image built and labelled with the version, the server installed in the container through MystTiq, the clone's world run there (REST answering, Day 173 11:02 read from its save) and stopped cleanly. | Done |
| W-1 | Read-only browser view | A browser page shows server status, players and backups. It has no write routes; every write request is refused. Remote sign-in uses the existing roles and TLS pinning. | Harness: every write route returns refusal in the browser session. Live: one remote browser session on the clone. Harness recorded (v1.0.3.0): 114 change routes refused. Live on the clone through loopback (sign-in, reads, a Start refused 403); a session from another computer over TLS is owed. Recorded (v1.0.6.0, 2026-10-06): from the Linux VM (another computer) to an isolated clone service on this PC over TLS through an SSH tunnel, the certificate pinned (a wrong pin refused): signed in, read the clone's status and 8 backups, a Start from an Admin browser session refused 403, signed out (then 401). Also this PC to a test service on the VM over pinned TLS. | Done |
| X-1 | Xbox player discovery | Establish what the save and REST data show for an Xbox player (identity, name, Pal and item data), read-only, using the owner's Xbox test account. Result recorded before any build. | Live discovery on the clone with the owner's account. Written findings in `docs/architecture/`. Blocked 2026-10-05: no Xbox session yet; the capture script and procedure are in `docs/architecture/v1.0.3.0-xbox-player-discovery.md`. | Blocked |

## v1.0.4.0 — Guarded save edits

Exit gate: S-1 `Done` (remove an item), S-2 `Done` (add an item), S-3 `Done` (Pal add and remove). No edit ships without its restore check.

Released 2026-10-05 as v1.0.4.0 with S-1 and S-2 `Built` (a player confirming in game on the clone is owed). S-3 moved to the milestone after v1.0.5.0: a Pal is two linked records (the parameter map and the Pal box slot) that must agree with the game's species record, so it ships later with its own restore check rather than without one.

Every edit in this milestone follows the same rules:
- An automatic backup is taken before the edit and is verified.
- The edit runs on a clone server first, never on a production world.
- A restore of that backup is run afterwards, and the restored world is compared with the pre-edit state.

| ID | Item | Acceptance | Evidence required | Status |
| --- | --- | --- | --- | --- |
| S-1 | Remove an item from a player | One named item is removed from one player's inventory. Other items are unchanged. The backup restores byte for byte. | Harness on a copy of a save. Live: clone server, player confirms, restore check. Recorded (v1.0.4.0): the logic harness, and on a copy of the clone's world one stack was removed and one added through the API, each after a fresh safety backup that passed its check, with only that slot changed; an item the world holds nowhere as a plain stack was refused, as was any edit while a server ran; and restoring the first edit's safety backup gave the pre-edit Level.sav byte for byte. A player confirming in game is owed. Recorded (v1.0.6.0, 2026-10-06) on the clone's real world: a stack removed, then PalServer loaded the world, saved and stopped; in the save the game wrote it was still gone; the safety backup restored the pre-edit Level.sav byte for byte. A player looking in game is owed. | Built |
| S-2 | Add an item to a player | One item is added to one player's inventory. Refused if the inventory is full or the player is online. | Same as S-1, plus a refused-case check. Recorded (v1.0.4.0) with S-1, plus the refusals (an item the world holds nowhere as a plain stack, a full inventory in the harness, any edit while a server runs). A player confirming in game is owed. Recorded (v1.0.6.0, 2026-10-06) with S-1: the added stack was still there in the save the game wrote. | Built |
| S-3 | Add and remove a Pal | One Pal is added to, and then removed from, one player. Pal data is checked against the game's own record. | Same as S-1, run on a clone with a test Pal. | Planned |

## Carried from v1.0 (evidence still owed)

These were `Built` or partly verified at 1.0. Each stays open until its evidence is recorded.

| ID | Item | Evidence still owed |
| --- | --- | --- |
| E-1 | Give Item/Pal and starter kits | A Pal delivery, a starter kit, and a refused give (wrong ID, offline player). The give and teleport paths are checked by `scripts/Test-v0.8.26.0-InGame.ps1`. |
| E-2 | Live map and teleport points | The player marker live on the map, a chat-triggered teleport, and point placement. One paired position was recorded on 2026-09-28. |
| E-3 | Notifications | Real Discord and email delivery confirmed in each channel. Overlaps with R-2. |
| E-4 | Linux desktop | File pickers, scaling, and other desktop environments. Window, clipboard, and tray already pass on the XFCE test VM. |
| E-5 | Linux service priority | Eco-to-normal priority recovery on a test install of the new unit. |
| E-6 | Themes | Supported modes across the app, and a Windows contrast theme run (`scripts/Test-v0.8.26.0-ContrastTheme.ps1`). |
| E-7 | Distribution | A launch on a machine that never had MystTiq or .NET, and source parity against the tag. |
| E-8 | Upgrade through a refused manifest | A MystTiq update meeting a refused Steam manifest, recorded live. |
| E-9 | Accessibility | A real screen-reader pass (Narrator, Orca) and focus order on every page. |
| E-10 | Translation review | Native review of all 11 non-English languages. Catalog coverage alone does not establish quality. |
| E-11 | Live identity guard | A second account joining the real server with a taken name. |
| E-12 | MystTiq self-update | A download from a real published release newer than the running build. |
| E-13 | Save Tools pip upgrade | An upgrade on a real Python install. |
| E-14 | Code signing | The first signed Windows release, once the SignPath variables are set. Not blocking. |

## v1.0.5.0 — MOD browser

Exit gate: M-1 is `Done`. Repositories to cover are agreed with the owner before work starts.

Repositories agreed 2026-10-06 (D-8). Released 2026-10-06 as v1.0.5.0 with M-1 `Built`: the real Nexus install with the owner's account is owed.

| ID | Item | Acceptance | Evidence required | Status |
| --- | --- | --- | --- | --- |
| M-1 | MOD browser (Nexus Mods and other repositories) | Search, read and install MODs from inside MystTiq. Builds on the Nexus catalog, the website-sourced MOD descriptions and the existing MOD install, update and rollback paths. | Harness with stand-in repositories. Live: one install on the clone through the real Nexus path, using the owner's account. Requested 2026-09-30. Recorded (v1.0.5.0): stand-in repositories in the ArtworkHarness; the archive check in the LogicHarness and through the service with real GitHub and Thunderstore archives; live, on the clone through an isolated service, the browser's own sources fetched a UE4SS MOD from a GitHub release (GuildFeedBox 0.4.1, from github.com only), the archive check read it as a UE4SS MOD in a subfolder, it installed as GuildFeedBox at the right depth, enabled and listed, and was removed again with the clone's MOD folders and mods.txt unchanged; Thunderstore's BasesPlus (a PAK with scripts) and ElementalRebalance (shimloader) were refused with their reasons. The Nexus install with the owner's account is owed. | Built |

## v1.0.6.0 — Carried and follow-ups

Exit gate: S-3 and M-2 `Done`. Agreed with the owner 2026-10-06. Released 2026-10-06 as v1.0.6.0 with M-2 `Done` and S-3 `Built` (a player seeing the Pal in game on the clone is owed).

| ID | Item | Acceptance | Evidence required | Status |
| --- | --- | --- | --- | --- |
| S-3 | Add and remove a Pal | Moved from v1.0.4.0 (see there). | Same as S-1, run on a clone with a test Pal. Recorded (v1.0.6.0): the logic harness; on a copy of the clone's world an Alpaca added and removed through the API with checked backups, the refusals, and the restore check; on the clone's real world a SheepBall added, kept by the game through a load and save, then removed and gone after another, and the pre-edit backup restored Level.sav byte for byte. A player seeing it in game is owed. | Built |
| M-2 | Install MODs laid out as the game folder | Archives the v1.0.5.0 check refuses today: a PAK with UE4SS scripts, LogicMods PAKs (with UE4SS's BPModLoader), each file placed where the game loads it, with rollback and removal. | Harness on the real layouts (BasesPlus). Live: one such MOD installed and removed on the clone. Recorded (v1.0.6.0): the logic harness on the real layouts (BasesPlus) and the MOD layout smoke with real archives; live on the clone, BasesPlus from the MOD browser's Thunderstore source installed as `LogicMods\\BasesPlus` (PAK and settings) and a UE4SS MOD, both listed, both removed, the clone unchanged. | Done |

## Backlog (not scheduled)

| ID | Item | Note |
| --- | --- | --- |
| B-2 | Anti-cheat level-gap scans from save data | Competitor gap (PalSupervisor). Needs real data to tune thresholds. Would be a candidate for the milestone after v1.0.4.0. |
| B-3 | Mod load order and profile export and import | Competitor gap (PalSupervisor). |
| B-4 | Discord control commands | Start, stop, restart and backup from Discord, with role checks. Confirm current Discord code first (alerts only today). |
| B-5 | Full web admin UI | Write access in the browser. Not planned: needs a hardened authentication model first. |

## Not planned

- Full save editor (broad edits of players, Pals and inventories). Out of scope under D-2.
- OS-level traffic shaping and per-process network accounting.
- A separate night HOST illustration and per-accent artwork (carried from the earlier optional list).
- Mobile apps.

## Current publication work

Signed release and the Linux package remain tied to the items above (E-14 and E-4). Keep public-release coordination separate from implementation status.
