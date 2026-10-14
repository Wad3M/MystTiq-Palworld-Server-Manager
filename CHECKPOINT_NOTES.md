<!-- MystTiq v1.0.7.0: file reviewed for this release (2026-10-08). -->
# MystTiq v1.0.7.0 Checkpoint: Errors Stay Visible, Nothing Is Lost

The first release from the outside review you sent (roadmap Q-1, Q-2, Q-3 and Q-5), with your decision on damaged
saved lists (D-10).

## What I found first

- **A damaged list could wipe your accounts.** If one of MystTiq's own lists (accounts, automation rules, kits and
  more) couldn't be read, MystTiq started that list empty. The next change then saved it over the file. I reproduced it
  on a real v1.0.6.1 service: three accounts, `users.json` cut short, a restart, one new account. Only the new one was
  left.
- **There was no last-chance error handler** anywhere in the app or the service, so an unexpected error closed the app
  and left nothing behind.

## Fixed in v1.0.7.0

- **Saved lists (your decision).** Every save keeps the previous file as a last good copy. A file that can't be read is
  copied aside, and the last good copy is loaded. If there is no good copy, changes to that list are refused with the
  reason, and you get a pinned alert with **Open Repair**. That opens Server Doctor with the list marked; its fix starts
  the list empty and keeps the bad file beside it.
- **Errors.** An unexpected error is recorded in `%LOCALAPPDATA%\MystTiq\crashes` and shown in a red banner instead of
  closing the app. The service records them too.
- **Background work.** The service's background jobs now log their failures once and stop cleanly at shutdown. An
  automation rule still running isn't started twice.
- **Retries.** Only a file another program holds is retried. A server state file that stays held now says what to
  check.
- **Default preset text.** The Default preset's description no longer mentions a diagnostic log.

## Verification

- **Reproduced first** on a real, isolated v1.0.6.1 service: three accounts, `users.json` cut short, a restart, one
  new account. Only that one was left on disk. The same steps on v1.0.7.0: the new account was refused with the reason
  and the file was left untouched.
- **Full gate** `scripts\Test-v1.0.7.0-Logic.ps1 -RunBuild`: 340 / 340 passed, 1 skipped. The skip is the v1.0.2.0
  system-unit test: the VM's test user has no passwordless sudo, and the per-user unit check covers it. Every earlier
  check is carried, the frozen v1.0.6.1 gate passes on its own checkpoint, and the Linux VM checks ran and passed.
  The gate ran twice: the second run was after removing three compiler warnings (unawaited tracker calls) that the
  package build showed.
- **Static gate:** 279 / 279. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Saved-lists route smoke** (an isolated service with sign-in on), all passed:
  - the last good copy loaded, and a named account signs in;
  - with no copy, `users.json` left byte for byte and the owner token still works;
  - a change refused inside a service comes back as HTTP 409;
  - the pinned alert links to the repair, and after the repair changes work again.
- **LogicHarness:** 178 scenarios, including:
  - the account wipe, the last good copy, the repair, a log starting empty;
  - one-at-a-time automation runs and background work logged once and stopped at shutdown;
  - the retry rule, and the server state file (concurrent, interrupted, held, read-only).
- **ArtworkHarness:** 822 checks in 12 languages, including:
  - an error thrown from a command is recorded, shown and leaves the app open;
  - Open Repair opens Server Doctor with the finding marked;
  - a refused change shows the service's reason.
- **Found on the way:** the gate's command-role audit cuts each command at its first comma (a `-contains` bug), so a
  call after a comma was never checked. Worked around here; offered as a separate task.

## For you

1. **Nothing to do** for your servers: the first save of each list starts its last good copy.
2. **Roadmap:** Q-1, Q-2, Q-3 and Q-5 are `Built`. Next is v1.0.7.1: Q-4 (every conflict refused) and Q-11 (alert
   storms bounded).
3. **Push:** this is ready to push and tag as v1.0.7.0 when you say so.
