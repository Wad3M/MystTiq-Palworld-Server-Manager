<!-- MystTiq v1.0.8.3: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.3 Checkpoint: The Map and World Pages, Tidied

## What changed

The fourth step of the split (C-3) gives two more areas their own files:
- **The Map:** its background, zoom and pan, the markers, and teleport points.
- **The world pages:** the Inspector and Pal editor, Bases, Guilds, character migration and world transactions.

Nothing looks or works differently. The main window's code files are down from 644,530 to 569,569 bytes.

## Verification

- **Full gate** `scripts\Test-v1.0.8.3-Logic.ps1 -RunBuild -RunLong`: 378 / 378 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.2 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 314 / 314. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the map's and world pages'
  checks pass against the new ViewModels (markers, Pals on the map, base and guild cards, Show on map).
- **The role table and the code-behind roles still match the code**, with the Bases page's handlers followed through
  the new page.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-3 is `Built`. Next is v1.0.8.4: the Server pages move out (C-4: setup wizard, Configuration, Launcher,
   Workspace).
4. **Push:** v1.0.7.0 to v1.0.8.3 are ready to push and tag when you say so.
