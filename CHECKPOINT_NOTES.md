<!-- MystTiq v1.0.0.6: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.0.6 Checkpoint: Buttons and Tags That Say What They Do

You asked for consistent buttons and tags, governed by the central look and not hard-coded, with colour: Delete red,
Open purple, Verify green, and other colours for the rest. Before this, each page picked its own button looks, more
than half of the main window's buttons (159 of 287) had none, and tags were drawn by hand: three coloured boxes per row on
Server Setup, four on Doctor, and fixed violet or orange text elsewhere whatever the state.

## Buttons

One table now gives every button its intent from its label, and only the style sheet colours it:

| Colour | Intent | For example |
|---|---|---|
| Red | danger | Delete, Remove, Revoke, Kick, Ban, Force Stop, Discard |
| Purple | open | Open, Browse, Show on map, Manage |
| Green | verify | Verify, Rescan, Recheck, Test, Validate, Run Doctor |
| Blue | apply | Save, Apply, Create, Add, Install, Send, Connect |
| Teal | info | Refresh, Preview, Load, Export, Copy |
| Amber | caution | Restore, Reset, Restart, Repair, Pause, Mute |
| Neutral | plain | Cancel, Back, Next, Dismiss |

- All 303 buttons in the window and its dialogs have one. The ribbon, map markers, list rows and section toggles keep
  their own shape.
- Server Setup's row buttons follow what they say: VERIFY green, MANAGE purple, INSTALL blue.
- To change a colour, or move a button to another intent, there's one place for each: the style sheet and the table.

## Tags

All 21 status tags are one shape, coloured by what they say: green (ready, pass, up to date, verified, healthy,
connected), amber (attention, update available, unverified), red (missing, failed, unreadable, not loaded, locked), grey
(disabled, optional, self-updating, not applicable) and blue for in-between states such as Starting.

## Verification

- **Full gate** `scripts\Test-v1.0.0.6-Logic.ps1 -RunBuild`: 284 / 284 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.5 check is carried, and the frozen v1.0.0.5 gate passes on its own checkpoint.
- **Static gate:** 234 / 234. It reads every button and tag in the XAML (303 buttons, 21 tags): all have an intent or a
  structural look, none sets its own colours, and none uses an old look class. **Validate-Release -Strict:** 0 errors, 0 warnings.
  **Distribution check:** passed.
- **ArtworkHarness:** 780 checks pass, including:
  - every visible button on every page has one intent, matching the table for its English label;
  - the same intent looks the same everywhere, and each intent has its own colour;
  - by hue: Delete red (350°), Open purple (268°), Verify green (154°);
  - the tags on Server Setup, Update Center, Doctor, Diagnostics, Backups and MODs match their status;
  - Server Setup's VERIFY, MANAGE and INSTALL rows; German.
- **Live (published v1.0.0.6, read-only):**
  - **Server Setup:** VERIFY green, MANAGE purple, READY tags green.
  - **Workspace:** Open and Browse purple, Save Paths blue.
  - **Update Center:** Up to date green, Update available amber (PalDefender 1.9.2 → 1.9.3), Self-updating muted (the neutral slate of a plain button, with dimmed text).
  - **Doctor:** PASS tags and Recheck green.
  - **Security:** Delete red, Reset Password and Enable / Disable amber, Create Account blue.
  - **Settings:** Forget red, Connect blue.
  - The connection tag at the top is green.
- **Found on the way:** the MOD status "Active / Unverified" would have read as green ("active"). It's now blue,
  in between, like Starting and Connecting.

## For you

- Look through **Server Setup, Workspace, Update Center, Doctor and Security** and say if any button's colour feels
  wrong for what it does. Moving it is a one-line change to the table.
- **Pushing:** v1.0.0.3, v1.0.0.4, v1.0.0.5 and v1.0.0.6 are committed but not pushed or tagged. Say when.
- **Next:** v1.0.1.0, an Update button on every Update Center row (greyed where it updates itself) and the first signed
  release.
