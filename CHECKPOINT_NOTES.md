<!-- MystTiq v1.0.5.0: file reviewed for this release (2026-10-06). -->
# MystTiq v1.0.5.0 Checkpoint: MOD Browser

The fourth milestone of your roadmap: M-1, the MOD browser. It covers the repositories you chose on 2026-10-06 (recorded
as D-8): your Downloads folder or other folders, Nexus Mods (free account), Thunderstore, CurseForge and GitHub releases.

## M-1 MOD browser — Built

**Mods > MOD Library > MOD Browser.** Pick a source and search:

- **Downloads and folders**: ZIPs that hold a MOD, newest first. Add or remove folders there.
- **Thunderstore**: Palworld's community. It is small, and its packages mostly need unreal_shimloader, which MystTiq
  does not manage, so the browser marks them.
- **CurseForge**: paste your API key from console.curseforge.com and Save Key. It is stored encrypted on this PC and sent
  only to CurseForge.
- **GitHub releases**: only repositories you add. An open GitHub search for Palworld MODs turned out to be mostly cheat
  and "free download" lure repositories, several pushed today, so there is none.
- **Nexus Mods**: uses your key from the Nexus Mods card. Your account is free, so files come through the site's Mod
  Manager Download button. Press **Allow MystTiq to Take Mod Manager Download Links** once. I haven't pressed it on this
  PC: it changes your Windows settings for nxm links.

**Install Selected File:**
- downloads only from that source's own sites;
- shows what the archive holds and whether MystTiq can install it;
- asks you, then installs as before, with the server stopped.

**Owed for Done:** one install on the clone through Nexus with your account.

## Archives are checked before any install

This also applies to a ZIP dropped on the page. Two problems turned up while building the browser:

1. A UE4SS MOD packed inside a folder (the usual layout) landed one folder too deep and did not load. It now installs
   from its own folder, under that folder's name.
2. Some archives were installed wrongly without a word:
   - a PAK with Lua scripts (the scripts were dropped);
   - a LogicMods PAK (put in ~mods);
   - several PAKs (they overwrote each other).

   These are now refused with the reason, as are shimloader packages, loader DLLs, archives containing a program and
   archives with no MOD. Nothing is written, and the refusal is in the Activity log.

Installing the refused layouts properly is proposed as **M-2** for v1.0.6.0, with **S-3** (Pal add and remove). That
milestone is a proposal for you to confirm.

## Verification

- **Full gate** `scripts\Test-v1.0.5.0-Logic.ps1 -RunBuild`: 312 / 312 passed, with the MOD archive smoke. The 5 Linux VM
  checks were skipped because 192.168.1.122 could not be reached. Every v1.0.4.0 check is carried, and the frozen
  v1.0.4.0 gate passes on its own checkpoint.
- **Static gate:** 258 / 258. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **MOD archive smoke** (isolated service, stand-in UE4SS):
  - a PAK and a UE4SS MOD packed in a folder install, the latter at the right depth and enabled;
  - eight wrong layouts are refused with nothing written, and each refusal is logged;
  - real archives: GuildFeedBox 0.4.1 from a GitHub release installs, while Thunderstore's ElementalRebalance
    (shimloader) and BasesPlus (a PAK with scripts) and PalDefender's own release ZIP are refused.
- **Live on your clone** (isolated service, port 18652, a throwaway token):
  - the browser's own sources found GuildFeedBox on GitHub and downloaded it from github.com only;
  - the check read a UE4SS MOD in a folder, and it installed as GuildFeedBox, enabled and listed;
  - it was removed again, and the clone's MOD folders and mods.txt match their before-state hash for hash;
  - BasesPlus from Thunderstore was refused on the clone with its reason;
  - CurseForge without a key asked for one.
- **Live look** at the published v1.0.5.0 desktop:
  - all five sources show in the picker;
  - a Thunderstore search listed 13 packages;
  - Install Selected File on BasesPlus downloaded it and refused it on this PC with the reason, before anything reached
    the server. Your own v1.0.0.0 service was connected; nothing was installed there.
- **LogicHarness:** the archive check on the real layouts. **ArtworkHarness:** 810 checks pass, including stand-in
  Thunderstore, CurseForge and GitHub, the host checks, a folder of ZIPs, the nxm handoff, the card and German.

## For you

- **M-1:** in MystTiq, press Allow MystTiq to Take Mod Manager Download Links (it names any program that has them
  now). Then on Nexus click Mod Manager Download for a Palworld MOD, and Install from link with the clone stopped and
  selected.
- **Optional:** a CurseForge API key, if you want CurseForge searched.
- **v1.0.6.0:** confirm or change the proposed milestone (S-3 Pal add and remove, M-2 game-folder layouts).
- **Still owed:**
  - S-1/S-2: an in-game check on the clone;
  - R-2: a channel and a test send;
  - R-3: the Linux VM;
  - W-1: TLS from another computer;
  - X-1: your Xbox account.
- **Pushed and tagged** with your go-ahead. This was the last version of the run you authorised (through v1.0.5.0).
