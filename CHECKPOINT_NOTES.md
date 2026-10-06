<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# MystTiq v1.0.6.0 Checkpoint: Pals in the Pal Box, MODs Laid Out as the Game

You approved the proposed milestone (S-3 and M-2) and asked for the owed evidence to be gathered automatically where
possible, with the VM on.

## S-3 add and remove a Pal — Built

- **Players > Pal box in the world save** (admins, under the inventory card): **Load Pal Box** lists the player's box,
  with each Pal's place, kind, level and name.
- **Remove Selected Pal** takes one Pal out. Party Pals and base workers are not touched here.
- **Add To Pal Box** adds a Pal of the kind picked beside it. It is a copy of a Pal of that kind already in a Pal box, so
  its stats are ones the game wrote. It gets a new id, the player as owner, the first free place and the player's guild,
  and not the original's name.
- **Guards:** the same as the inventory edits. The server must be stopped; you preview and confirm; a checked safety
  backup comes first; the edit is verified to change exactly that Pal; any failure is rolled back.
- **Live on your clone's real world:** I added a SheepBall, then the clone's PalServer loaded the world, saved and
  stopped, and the game's own save still had it. I then removed it, ran the server again, and it was gone, with the box
  exactly as before. Restoring the pre-edit backup gave the original Level.sav byte for byte.
- **Owed for Done:** a player seeing it in game.

## M-2 MODs laid out as the game folder — Done

- Blueprint MODs for UE4SS (LogicMods) install into `Paks\LogicMods\<name>` with their files.
- A PAK with UE4SS scripts installs as two parts under one name. Both parts show on the MODs page and can be removed.
- Both need UE4SS. If BPModLoaderMod is off in mods.txt (it is on your clone), the install says so; MystTiq does not
  switch it on for you.
- **Live on your clone:** BasesPlus from the MOD browser's Thunderstore source installed both parts. I then removed both,
  and the clone is unchanged.

## Evidence gathered automatically (#3)

| Item | Result | Status |
| --- | --- | --- |
| R-3 Linux service under systemd | Your production unit came up 11 s after the VM booted, with PalServer. The current build, as a per-user unit, was restarted after a kill and stopped cleanly (8/8). | **Done** |
| W-1 Read-only browser view | From the VM, a pinned-TLS session on your clone: sign-in, status and 8 backups read; a Start refused (403); sign-out. | **Done** |
| R-2 Alert delivery | A webhook delivered (Delivered), then a closed port (Failing, with the reason). | Built (Discord and email owed) |
| S-1/S-2 Inventory edits | The game kept both edits through a real load and save; the restore was exact. | Built (an in-game look owed) |
| X-1 Xbox | Needs your Xbox. | Blocked |

How these were run:
- The production unit on the VM was only read. Its test user has no passwordless sudo, and a reboot would interrupt your
  production PalServer there, so I used a per-user unit instead.
- The VM's firewall allows only SSH, so the TLS sessions ran through SSH tunnels. No firewall was changed on either
  machine.
- One restore left a 0.6 MB temporary copy of three of the game's own September backups in the clone's `Pal\Saved`
  folder (`SaveGames.restore-rollback-…`). Those files belong to Administrators, so I can't delete them. All 21 files are
  verified duplicates of files in the restored world. **Fix Save Folder Access** (or deleting the folder as admin) clears
  it.

## Fixed on the way

- A file held for a moment (an antivirus scan of a fresh save) could fail a save edit's last step, which then rolled back
  for nothing. Seen once in the Pal box smoke. MystTiq now waits a moment and retries.
- A restore that cannot remove its temporary copy because of files owned by administrators now says so and points to
  Fix Save Folder Access.

## Verification

- **Full gate** `scripts\Test-v1.0.6.0-Logic.ps1 -RunBuild`: 324 / 324 passed. With the VM on, every VM check ran:
  - remote sign-in in four roles from Windows and Linux;
  - the Desktop in the VM's real XFCE session;
  - core pinning and the unit check;
  - the per-user systemd unit.

  Only the v1.0.2.0 system-unit test was skipped: it needs passwordless sudo, which the VM's test user does not have.
  Every v1.0.5.0 check is carried, and the frozen v1.0.5.0 gate passes on its own checkpoint.
- **Static gate:** 264 / 264. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Pal box smoke** (a copy of the clone's world): read the box (37 Pals, 27 kinds addable); an Alpaca added and removed
  with the box exact each time; the refusals; nothing edited while a server runs; the restore check; the activity log.
- **MOD layout smoke:** LogicMods and PAK-with-scripts install, are listed and removed, and need UE4SS and a free name;
  the v1.0.5.0 refusals still hold; real GuildFeedBox and BasesPlus install; shimloader and PalDefender are refused.
- **Live on your clone:**
  - S-3 through two real game load-and-save cycles, and the restore exact;
  - S-1/S-2 through one cycle, and the restore exact;
  - M-2 with BasesPlus, installed and removed, the clone unchanged;
  - W-1 from the VM over pinned TLS.
- **Live on the VM:** R-3 as a per-user unit (8/8); the production unit's boot start observed; the production unit
  unchanged.
- **Live look** at the published v1.0.6.0 desktop: the Players page's Pal box card under the inventory card.
- **LogicHarness:** the Pal box edit and the M-2 layouts. **ArtworkHarness:** 811 checks, including the Pal box card.

## For you

- **S-1/S-2/S-3:** join the clone and look at an edited inventory or Pal box (all three close to Done).
- **R-2:** a Discord or email channel and a test send.
- **M-1:** the Nexus install with your account.
- **X-1:** your Xbox in the clone.
- **Optional:** Fix Save Folder Access, which also removes that 0.6 MB leftover.
- **Push:** this is ready to push and tag as v1.0.6.0 when you say so.
