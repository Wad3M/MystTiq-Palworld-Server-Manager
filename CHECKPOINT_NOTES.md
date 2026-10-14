<!-- MystTiq v1.0.8.1: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.8.1 Checkpoint: HOST and Dashboard, Tidied

## What changed

The second step of the split (C-1):
- **The HOST page** (machine, priority and eco mode, history, network limits) now lives in its own file,
  `HostPageViewModel`.
- **The Dashboard's cards** (the texts, the recent activity, the addresses line) now live in `DashboardViewModel`. The
  health light stays with the main window, because it follows the server starting and stopping.

Nothing looks or works differently. The main window's code files are down from 732,039 to 704,238 bytes.

## Verification

- **Full gate** `scripts\Test-v1.0.8.1-Logic.ps1 -RunBuild -RunLong`: 372 / 372 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried (the HOST, history, bandwidth, cores,
  server-name and addresses contracts now read the new files), the frozen v1.0.8.0 gate passes, the long set passes, and
  the Linux VM checks ran and passed.
- **Static gate:** 308 / 308. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the HOST checks pass against
  the new ViewModel (the readings, unsaved edits kept, validation, the bandwidth suggestion, the history summary).
- **The role table still matches the code**, with the HOST commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) are still going; I'll collect them after 2026-10-13 03:00 UTC.
3. **Roadmap:** C-1 is `Built`. Next is v1.0.8.2: the Players page moves out (C-2).
4. **Push:** v1.0.7.0 to v1.0.8.1 are ready to push and tag when you say so.
