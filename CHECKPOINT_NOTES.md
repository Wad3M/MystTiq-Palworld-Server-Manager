<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.4.0 Checkpoint: Guarded Save Edits (Items)

The third milestone of your roadmap: S-1 remove an item, S-2 add an item, S-3 add and remove a Pal. Decision D-2 holds:
guarded edits only, each with a backup first and a restore check.

## S-1 remove an item, S-2 add an item — Built

- **Players > Inventory in the world save** (admins): **Load Inventory** shows the selected player's main inventory
  slot by slot, as the world save holds it.
- **Remove Selected Stack** takes one stack out. **Add To Inventory** puts the item picked in the list above into the
  first free slot, with the amount from the amount box.
- Every edit:
  - runs only while the server is stopped;
  - is previewed, and you confirm it (red for a removal);
  - takes a fresh safety backup that must pass its check before anything is touched;
  - is decoded again and must show exactly that one change, then replaces the world in one step;
  - puts the original back on any failure;
  - is in the Activity log with the backup's name.
- Only plain stacks: coins, ores, Pal Spheres, food and the like. Tools, weapons, armour and eggs carry their own record
  in the save and are left alone. An item can be added only if the world already holds it as a plain stack.
- **Owed for Done:** on the clone, a player confirms in game that the stack is gone, then that the added one is there.

## S-3 add and remove a Pal — moved

A Pal is two linked records (the character entry and its Pal box slot) whose fields must agree with the game's own
species record. Per the roadmap rule it moves to the milestone after v1.0.5.0 with this note, rather than shipping
without its restore check.

## Fixed on the way

After a PalServer crashed or was ended outside MystTiq, backup restores, save edits and MOD changes kept answering "Stop
PalServer first" until the server was started again. Pressing Stop on the crashed server didn't clear it. The status
kept the dead process's id, and 23 guards counted that as running. They now look for a live process or an open game
port. The inventory smoke found it.

## Verification

- **Full gate** `scripts\Test-v1.0.4.0-Logic.ps1 -RunBuild`: 306 / 306 passed, with the inventory smoke. The 5 Linux VM
  checks were skipped because 192.168.1.122 could not be reached. Every v1.0.3.0 check is carried, and the frozen
  v1.0.3.0 gate passes on its own checkpoint.
- **Static gate:** 253 / 253. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Inventory smoke** (a copy of the clone's world, the real save tools, an isolated service):
  - a player's main inventory read from the save (5 slots used);
  - one stack removed and one added, each after a fresh safety backup that passed its check, with only that slot changed;
  - an item the world holds nowhere as a plain stack refused;
  - any edit refused while a PalServer ran from the server folder;
  - with that server killed, the status still named the dead process (the case behind the fix), and restoring the first
    edit's safety backup gave the pre-edit Level.sav byte for byte;
  - every edit in the Activity log.
- **LogicHarness:** the inventory edits (remove, add, every refusal, only that slot changed) and the crashed-status rule.
  **ArtworkHarness:** 795 checks pass, including the inventory card.
- **Live look** at the published v1.0.4.0 desktop: Players > Inventory in the world save, with Load Inventory, a red
  Remove Selected Stack and Add To Inventory.
- Your own MystTiq (v1.0.0.0) was running during the gate; nothing of it was touched.

## For you

- **S-1/S-2:** on the clone, with the server stopped, remove a stack from your character and add one, start it, and
  tell me what you see in game.
- **Still owed:** R-2 (a Discord or email channel and a test send), R-3 (the Linux VM switched on), W-1 (the browser view
  from another computer over TLS), X-1 (your Xbox account in the clone).
- **Pushed and tagged** with your go-ahead (push through v1.0.5.0).
- **Next:** v1.0.5.0, the MOD browser. The roadmap says its repositories are agreed with you before work starts.
