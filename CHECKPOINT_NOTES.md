# MystTiq v0.9.9.0 Checkpoint: One Helper, Crash Causes and Local Formats

The accepted baseline stays **v0.8.25.0** until you accept this one.

Full detail: `docs/architecture/v0.9.9.0-helper-crash-formats.md`. This is "continue on any outstanding roadmap and
fixes": the open items that needed no one but the code, plus two bugs found on the way.

## Two bugs found and fixed

Both showed up on your Linux VM, where port 8213 is held by the older `/opt/mysttiq` service.

- **Adding a server could take your running servers down.**
  - The new-server wizard restarts MystTiq's local helper service to bring the new server online.
  - That restart killed the helper's whole process tree, which includes every game server it had started. Crash
    recovery then restarted them, so players would have been disconnected.
  - Now only the helper stops. Servers keep running and the next helper adopts them.
- **Helpers piled up.**
  - When the usual port was held by another MystTiq version, the desktop started a new helper on every launch.
  - Several then watched the same servers: two within a minute on the VM.
  - The desktop now records the helper it started and reuses it, across app restarts too.
  - Found on the VM check: the installed service there has TLS on, so its port read as free and the helper was
    started on a port it could not have. A port that accepts connections now counts as taken.

## Roadmap items closed

- **Crash analysis names a server that dies when a player joins.**
  - It's built from your own PalDefender 1.8.3 crash on 2026-09-28: that session's log just ends on "connected to the
    server".
  - It only reports a session that ended within 5 minutes of the join. It never judges the newest log of a running
    server, and it leaves the player's address out.
- **UE4SS is compared automatically.**
  - The installed `UE4SS.dll` is compared by content with the three newest releases' downloads.
  - An identical file names its release.
- **Alerts for every update** (your request of 2026-09-29).
  - The Alert Center's out-of-date alert now covers a new game server build, a newer UE4SS release, PalDefender, and
    every installed MOD with an update (named, up to five).
  - Each sends one alert, reminders while it lasts, and a "Resolved" notice.
- **The Doctor fixes a port mismatch.** Fix sets the advertised `PublicPort` to the port the server really uses.
- **Numbers, dates and times follow the language you chose.**
  - They used to follow Windows' language.
  - Only the formats change: text comparison stays as it was, which avoids the Turkish "i" problem.
- **A distribution check.** `Test-v0.9.9.0-Distribution.ps1` verifies the checksums, the ZIP contents, the versions on
  the binaries, and that the packaged service starts from a clean folder.
- **Roadmap tidied.** The stale rows and the "publish v0.9.2.0" note are updated.

## Verification

- **Full gate:** 235/235, nothing skipped (`gate990-full.txt`).
  - The first full run was 234/235: the v0.9.5.0 upgrade smoke's restore was refused once ("Stop PalServer before
    restoring a backup"). Most likely its stop arrived while the service was still starting the stand-in server; I
    could not reproduce it. The smoke now repeats the stop until the server stays down. No product code changed for it.
- **Clean and strict validation:** 0 errors, 0 warnings.
- **Distribution check:** 4/4 on the packaged ZIPs.
- **Route smoke:** 3/3.
- **Windows, live:** one helper; it is recorded and the same one is reused after closing and reopening the app. The
  clone server was untouched.
- **Linux VM, live:** four helpers left by v0.9.7.0 and v0.9.8.0 were stopped by hand. v0.9.9.0 then started one
  helper on a private port (8213 belongs to the installed v0.7.62.0 service), and closing and reopening the app reused
  that same helper. After the port fix, a first launch with no record also went straight to a private port.
- **Not checked live:** the UE4SS and MOD alerts firing on a real update. The harness covers the rules; your clone's
  UE4SS reads "check manually", which by design raises no alert.

## For you

- **Your clone's UE4SS is none of the three newest builds.** The Update Center still reads "check manually" for it, now
  with that reason. Reinstalling UE4SS through MystTiq's own install flow would make it trackable.
- **Still yours to do:**
  - click **Allow through Firewall** once;
  - native review of the translations;
  - a screen-reader pass;
  - the Discord and email delivery checks;
  - decide whether to update your main server (one build behind) and what to do with the Frostbound profiles;
  - push and tag v0.9.0.0 through v0.9.9.0.
