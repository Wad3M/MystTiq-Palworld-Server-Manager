<!-- MystTiq v1.0.1.0: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.1.0 Checkpoint: Update, on Every Row

You asked for an Update button on every Update Center row: greyed out where the component updates itself, but always
there. Before this only pip had one, and PalDefender's row told you to swap its DLLs by hand.

## What each row's Update does

| Row | Update |
|---|---|
| MystTiq | Downloads the new release, checks it against the release's checksum list, unpacks it into a new folder beside this one and opens it. Exit from the tray and start the new one; settings and servers carry over. Greyed out when there's nothing newer. |
| SteamCMD | Greyed out: it updates itself every time it runs. If it's missing, Update installs it. |
| Palworld server | SteamCMD update (server stopped). |
| UE4SS | Opens the UE4SS page with the newest release selected and the install previewed; you click Apply. |
| PalDefender | Replaces `PalDefender.dll` and `d3d9.dll` from its newest release, with the server stopped. `d3d9_config.json` and the PalDefender folder are left alone, the old files are kept in a backup, and if you switched it off it stays off. |
| pip, Save Tools | Upgraded with pip. |
| Python, .NET, VC++, Build Tools, PlM/Oodle | Opens the official download page. Python stays on 3.10, because the save decoder is built for it. |

Greyed-out buttons say why right beside them, and every Update says what it does when you hover over it.

## Fixed on the way

- The PlM/Oodle row never had its link. It was looked up as "PIM" (capital I), and the page it pointed to doesn't exist.
  It now opens the decoder's project, which its install record names.

## Signing

Nothing in the code waits on it. The release workflow signs automatically once the SignPath variables are set in the
repository, and until then it packages unsigned as before.

## Verification

- **Full gate** `scripts\Test-v1.0.1.0-Logic.ps1 -RunBuild`: 289 / 289 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.6 check is carried, and the frozen v1.0.0.6 gate passes on its own checkpoint.
- **Static gate, after your roadmap rewrite:** 239 / 239. The roadmap check now reads your new structure (current version, the v1.0.2.0 to v1.0.5.0 plan, owner decisions). The v1.0.0.0 checks read the history file too. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **LogicHarness:** the PalDefender update scenario passes (the files it writes, a checked download, settings and backup kept, refusals that change nothing, and switched-off PalDefender staying off).
- **ArtworkHarness:** 789 checks pass, including:
  - every one of the 12 rows has Update, greyed out only for SteamCMD, an up-to-date MystTiq and what doesn't apply;
  - the reasons and tips; UE4SS's Update landing on its page;
  - MystTiq's download: unpacked beside the folder, a second time into "-2", and refused on a bad or missing checksum;
  - German.
- **Live, on your real PalDefender files (copied to a temp folder):** the real GitHub release updated the copy from
  1.9.2 to 1.9.3. The version check passed, `d3d9_config.json` was unchanged and the old files went to a backup. Your
  live server is still on 1.9.2.
- **Live, published v1.0.1.0:** all 12 rows show Update. MystTiq ("newest") and SteamCMD ("updates itself") are greyed
  out with the reason beside them. PalDefender shows Update available, 1.9.2 → 1.9.3. Clicking UE4SS's Update opened
  the UE4SS page with 2281fa31 selected and the install previewed; nothing was applied.
- **Every official page opens** (python.org, Microsoft, .NET, PyPI, GitHub). The old PlM/Oodle link was a 404.

## For you

- **Update PalDefender:** your server has 1.9.2 and 1.9.3 is out (it fixes several crashes). Stop the server, then click
  Update on the PalDefender row. I tested it on a copy of your files but did not touch your live server.
- **Pushed and tagged** with your go-ahead (push through v1.0.5.0).
- **Publishing:** MystTiq's own Update only offers published releases. Your GitHub releases since v1.0.0.0 are still
  drafts, so it won't offer them until you publish them.
