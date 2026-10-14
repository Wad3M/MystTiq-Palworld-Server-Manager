<!-- MystTiq v1.0.9.2: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.9.2 Checkpoint: MOD Load Order and Profiles

## What changed

The MOD Library has a new **Load Order and MOD Profiles** card.

- **UE4SS load order.** Your UE4SS MODs, in the order they load (top to bottom). Use the arrows to move one, then **Save
  Load Order**. It takes effect at the next server start, and MystTiq keeps the previous order file as a backup.
- **PAK MODs** are listed with a short explanation of why they have no order to set. When two PAKs change the same thing
  in the game, the game decides, and renaming files does not help.
- **MOD profiles.** **Export Profile…** saves a small file listing every MOD: whether it is on, the UE4SS order, the
  version, where it came from, and a check of its files. It never contains the MOD files themselves. **Import
  Profile…** first shows what would change. **Apply Profile** then switches MODs and sets the order for the MODs this
  server has, and lists any it is missing with where to get them.

The server must be stopped to change the order or import a profile. The new text is in all 12 languages.

## Verification

- **Full gate** `scripts\Test-v1.0.9.2-Logic.ps1 -RunBuild -RunLong`: 402 / 402 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.9.1 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 337 / 337. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **B-3 route smoke** on a real isolated service (4 / 4): the load order read with the PAK listed; a new order moves only
  the MOD lines and keeps mods.txt.previous; a stale list and a Viewer are refused with nothing changed; a profile
  exports with hashes, previews, applies, lists the missing MOD; a file that is not a profile is refused in words.
- **LogicHarness** (the three new B-3 scenarios included) and **ArtworkHarness** (12 languages, the new card) pass.

## For you

1. **The join problem** you reported is not in this release. I'm waiting on the double-click test I described (stop
   the server, double-click PalServer.exe, join) to know whether it is MystTiq's launch or the game's account
   handling.
2. **To try the new card:** stop the server, open MOD Library, expand Load Order and MOD Profiles, then move a UE4SS MOD
   and save, or export a profile.
3. **Next:** v1.0.9.3, level-gap scans (B-2).
4. **Push:** v1.0.7.0 to v1.0.9.2 are ready to push and tag when you say so.
