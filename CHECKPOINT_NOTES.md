<!-- MystTiq v1.0.0.5: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.0.5 Checkpoint: Bases and Guilds, Each in Its Own Way

You said the Bases and Guilds pages looked too similar and had redundant data. They were one page underneath: the same
three count cards, the same "Evidence Model" text, and lists that both repeated the guild's name, 32-character ID and
leader.

## Bases: places

- One card per base: **where it is** (map coordinates, as on the Map page), **how many Pals work there** and which kinds
  ("Lamball ×2 · Cattiva · Depresso · +1"), and the owning guild.
- Select a base for every Pal working there (name or nickname, level, owner), plus **Show on map** (opens the Map zoomed
  to that base) and **Open guild**.

## Guilds: people

- One card per guild: the name, the leader (♛), "3 members · 2 bases", and how many are online.
- Select a guild for its **roster by name**: the leader first, then who's online, then everyone else with when they were
  last saved. Its bases are listed with their locations, and **Open base** jumps to each one.

## Both

- The shared count cards and the Evidence Model block are gone. Each page opens with one summary line, for example
  "2 bases · 1 on the map · 14 Pals working". The warnings card only shows when there are warnings.
- The right-click menus, base transfer, base recovery and guild operations are unchanged.

## Verification

- **Full gate** `scripts\Test-v1.0.0.5-Logic.ps1 -RunBuild`: 279 / 279 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.4 check is carried, and the frozen v1.0.0.4 gate passes on its own checkpoint.
- **Static gate:** 229 / 229. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **ArtworkHarness:** 768 checks pass, including:
  - the base-card and roster rules on sample data;
  - both pages rendered, with no shared count cards or Evidence Model block;
  - Show on map, Open base and Open guild landing on the right page (this check failed with the first version, see below);
  - German.
- **Live on your data (read-only):**
  - **Bases:** "2 bases · 2 on the map · 8 Pals working", cards at (248, -495) and (144, -569), and the 8 working Pals with levels.
  - **Guilds:** MystTik led by Melly, the roster (Melly, Melly, Spiral, Wade, each with when they were last saved), and both bases with Open base.
  - **Show on map** opened the Map zoomed 4× on the base.
- **Found and fixed while checking:** Show on map first did nothing. It passed the page in a form the navigation ignores. It now navigates by page name, and the harness runs all three links.

## For you

- Open **World > Bases** and **World > Guilds**; try **Show on map** on a base.
- From v1.0.0.4, still yours: **Backups > Fix Save Folder Access** (Windows asks first), then a restore if you want to
  check the day in the game.
- **Next:** v1.0.1.0, an Update button on every Update Center row (greyed where it updates itself) and the first signed
  release.
- **Pushing:** v1.0.0.3, v1.0.0.4 and v1.0.0.5 haven't been pushed. Say when.
