<!-- MystTiq v1.0.0.1: file reviewed for this release (2026-10-04). -->
# MystTiq v1.0.0.1 Checkpoint: Launcher, Identity Guard, Tray and Stuck Starts

This is your v1.0.0.1 (from `MystTiqPalworldServer_v1.0.0.1_UIConsoleSteamPolish_FULL_SOURCE.zip`) merged with today's
fixes, and the features from 30 September. It is not published or pushed.

## Your v1.0.0.1, merged

- Merged against v1.0.0.0 with no conflicts; it compiles.
  - Your other tool renamed "1.0.0.0" to "1.0.0.1" everywhere, including history comments such as "v1.0.0.0: code
    signing". Lines whose only change was that rename were left as they were.
- Fixed on the way:
  - Launcher-page inputs had no accessible names (the ArtworkHarness stops on that).
  - The effective command line was passed through the translator; it is now shown as is, and the notes under it are
    translated.
- Your 112-check gate dropped every earlier regression check. The v1.0.0.1 gate is generated from the full v1.0.0.0 gate
  instead, with your Launcher checks added.

## Bug: launching through MystTiq changed players' characters

- **What happened:**
  - Palworld derives each Steam player's ID from their Steam ID: Wade is 67D8D355 and Melly is A3835C7B.
  - In some sessions the server gave them other IDs, so they got the new-character screen. Their real characters were
    never touched.
- **The cause is the launch arguments.** You confirmed it: double-clicking `PalServer.exe` keeps the characters, and a
  script with MystTiq's arguments gave the same wrong ones.
  - Which argument does it is not known. Every bad start had `-port=8211`, `-stdout` and `-FullStdOutLogOutput` in common.
- **The fix:**
  - **No arguments by default.** A server without saved Launcher settings starts with no arguments at all, just like a
    double-click. `-port=` is added only for a server that isn't on 8211.
  - **A Like double-click preset** on Server > Launcher. It also starts the server through Windows with a normal window,
    the way a double-click does.
- **Your main server has saved Launcher settings** (Show Window, saved today at 12:41) that still pass `-port=8211 -log
  -stdout -FullStdOutLogOutput -abslog=…`. Saved settings win over the default. After installing this build, open
  **Server > Launcher**, click **Apply Like double-click**, then **Save Launcher Settings**.
- **Safety net:** the identity guard, on by default, catches any player who still doesn't get their character. It kicks
  them before a duplicate is made and records it.
- **The extra characters** (E290DA9A, 014308E2, 1467C601, 84544311) are still in the world, for you to keep or delete.
## Bug: mysttiq-server.exe left running after closing

- Closing the window now always goes to the tray.
- **Exit** (or Force Exit) in the tray stops every server the helper runs, then the helper, then the app.
- "Exit GUI only" is gone.

## From 30 September

- **Stuck starts:** after two minutes without its port, the Dashboard says how long it has been starting and offers
  **Test without MODs** or **Find the MOD** (one at a time).
- **Addresses:** the Dashboard lists the local addresses and the public address, each with the port.

## Verification

- **Full gate** `scripts\Test-v1.0.0.1-Logic.ps1`: 257 / 257 passed; the 4 Linux VM checks were skipped because 192.168.1.122 could not be reached.
- **Static gate:** 210 / 210. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** 4 / 4.
- **Stuck-start smoke** `Test-v1.0.0.1-RouteSmoke.ps1`: 4 / 4. This includes the check that the stand-in server received only `-port=18711`.
- **ArtworkHarness:** 752 checks passed, including Dashboard addresses, the stuck panel and German.
- **Live check:** closing the published desktop window sent it to the tray. The process stayed alive with its window hidden, its helper stayed up and healthz reported 1.0.0.1. **Exit** from the tray menu was not clicked live. The logic gate covers it: every running fleet server is stopped, then the owned helper.
- **Not yet verified:** a player joining after **Like double-click** is saved. This needs you.

## For you

- **Apply Like double-click** on Server > Launcher for the main server and save, as above. Then have a player join.
- **Next bad join:** the identity guard records it with the details needed to find the cause.