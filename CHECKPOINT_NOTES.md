<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# MystTiq v1.0.6.1 Checkpoint: Your Character, Not a New One

You reported joining from another computer with your own Steam account and getting a new character ("Unknown Player")
instead of Wade, while a hand start gave you Wade.

## What I found

- **Your Steam account still maps to Wade.** It is `steam_76561197962020201`, and Palworld's Steam rule gives that
  account `67D8D355`, Wade. The server gave it `E290DA9A`, the same wrong ID as in the v1.0.0.1 incident. Your character
  was never touched.
- **An older service was running your server.** Your desktop was v1.0.6.0, but the service running the server was
  **v1.0.0.0**, left running since 5 October. The desktop simply connected to it. So nothing since v1.0.0.0 was running
  for your server, including the identity guard that would have kicked you before a new character was made.
- **It started PalServer with the options behind wrong characters.** Your main server's saved options are `-port=8211
  -log -stdout -FullStdOutLogOutput -abslog=…`. Every wrong-character start had those; double-click starts didn't.
  Every Launcher preset except **Like double-click** was still switching them on. The Launcher page also showed them
  switched on for a server with no saved launch settings, so saving that page wrote them in. That's likely how they got
  onto your main server.
- **What I'm not sure of:** on 11 August, before MystTiq's Launcher existed, Melly got a wrong ID while you got the right
  one in the same session. The options are the strongest lead, not a proven sole cause.

## Done on your PC (with your OK)

The old v1.0.0.0 service was stopped while PalServer was stopped. Your desktop starts its own service the next time it
connects. I did not change your saved launch options: you didn't ask for that.

## Fixed in v1.0.6.1

- **Older service warning.** When the service on your PC is older than the app, the Dashboard says so in red. **Update
  Service To This Version** stops the old one and starts the app's own. A running PalServer keeps running, and players
  stay connected.
- **Launcher presets.** No preset adds `-log`, `-stdout`, `-FullStdOutLogOutput` or `-abslog` any more, a server with no
  saved settings shows them off, and the Launcher warns when they're on.
- **Identity alert.** It names the options the server started with and says to choose Like double-click.

## Verification

- **Full gate** `scripts\Test-v1.0.6.1-Logic.ps1 -RunBuild`: 322 / 322 passed. The 6 Linux VM checks were skipped because the
  VM was switched off by then (all of them passed for v1.0.6.0 a few hours earlier). Every v1.0.6.0 check is carried, and the
  frozen v1.0.6.0 gate passes on its own checkpoint.
- **Static gate:** 267 / 267. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Validator fix:** the first fourth-number release after v1.0.6.0 flagged that version's own test scripts as stale.
  Scripts named for their own release may now name it.
- **LogicHarness:** the alert's advice for your actual saved options (named, with Like double-click advised) and for none.
- **ArtworkHarness:** 815 checks, including:
  - an older local service flagged, while the same, newer, remote or unknown versions are not;
  - the red Dashboard banner with Update Service To This Version, gone with the app's own version;
  - no preset or default switching on the four options;
  - the warning when one is on.
- **Live:** with your OK, the v1.0.0.0 service was stopped while PalServer was stopped. Your join as Wade is still owed.

## For you

1. **Launcher:** on **Server > Launcher**, click **Like double-click**, then **Save Launcher Settings**.
2. **Test:** start the server from MystTiq and join from the other computer. You should be Wade. Tell me what you see.
3. **Leftover:** the new "Unknown Player" character (E290DA9A) is harmless. Leave it, or I can remove it later with a
   backup first, if you say so.
4. **Push:** this is ready to push and tag as v1.0.6.1 when you say so.
