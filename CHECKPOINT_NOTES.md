<!-- MystTiq v1.0.7.4: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.7.4 Checkpoint: Secrets and Archives

## What changed

- **Secrets are encrypted on disk (Q-14).**
  - The SMTP password, the Discord and webhook URLs and the Discord bot token are encrypted on Windows, for this computer
    only. On Linux their files are readable by their owner only.
  - The first start of the new service moves your current settings over and keeps one plain copy beside each file.
  - Server Doctor lists that copy under Security, with **Delete Plain Copy**.
- **Every archive is checked first (Q-15).** MOD installs, MOD and UE4SS snapshot restores, the UE4SS download, backup
  restores and MystTiq's own update are refused before anything is written if they have over 20,000 files, expand past
  8 GiB, or would not fit on the disk.
- **Secrets stay out of logs (Q-9).**
  - A failed Discord or webhook send is logged by its address only, not the full URL with its token.
  - Password-style start arguments are hidden in the console log.
  - Found while doing this: any Viewer could read the channel settings, webhook URLs and SMTP password included,
    through the API. That now needs Admin, and the password is never sent back.

## Verification

- **Full gate** `scripts\Test-v1.0.7.4-Logic.ps1 -RunBuild`: 360 / 360 passed, 1 skipped (the v1.0.2.0 system-unit
  test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.7.3 gate passes, and the Linux
  VM checks ran and passed.
- **Static gate:** 297 / 297. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Secrets at rest** (LogicHarness): a plain settings file from an older version is encrypted at start-up, reads back
  the same, keeps one plain copy, stays encrypted after a save and after a restart.
- **Secrets in logs** (LogicHarness): a failed send to a closed port leaves only `http://127.0.0.1:<port>/****` in the
  activity log; password-style start arguments are hidden; the channel settings the API returns carry no SMTP password,
  and saving them back keeps it.
- **Archive limits** (LogicHarness): too many files or too much expanded size is refused with the reason; a normal
  archive passes; a broken one says so.
- **Roles:** the gate reads every fleet route that changes something; all have a role except the four sign-in routes.
- **LogicHarness:** 189 scenarios. **ArtworkHarness:** passes in 12 languages.

## For you

1. **To test:**
   1. Update the service (the Dashboard offers it).
   2. Send a test notification from Alert Center and check that it arrives where it used to. If you use the Discord bot,
      check that it is online.
   3. Then open Server Doctor and click **Delete Plain Copy**.
2. **Roadmap:** Q-14, Q-15 and Q-9 are `Built`. Next is v1.0.7.5: Q-10 (long-running, stress and concurrency tests).
3. **Push:** v1.0.7.0 to v1.0.7.4 are ready to push and tag when you say so.
