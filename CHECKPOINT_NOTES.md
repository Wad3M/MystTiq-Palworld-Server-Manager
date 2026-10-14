<!-- MystTiq v1.1.0.0: file reviewed for this release (2026-10-12). -->
# MystTiq v1.1.0.0 Checkpoint: The New Accepted Baseline

## What changed

**v1.1.0.0 is the new accepted baseline**, in place of v1.0.0.0, as you asked. The code is v1.0.9.3's; this release is
the documentation pass and the baseline.

**The documentation pass.** I read every user and developer page against the app and fixed what had drifted:
- **README:** 20 stacked "Also new" paragraphs became one "What's new in v1.1.0.0" by theme. The feature table now
  lists what shipped since 1.0, and the release history is grouped by series.
- **Website:** the milestones still showed "NEXT: MOD browser", which shipped in v1.0.5.0.
- **Linux pages:** they still described the original v0.3 plan. They now cover how to run the service today (systemd,
  per-user unit, `service-run`, Docker), what is tested, and what is still open. The old plan moved to history. A new
  check keeps the Linux command list matched to the real commands.
- **Roadmap, publishing guide, checklist and indexes:** brought up to date.

**The 24-hour runs passed:**
- Windows: 24 h, memory +1.5 MB, 16,600 requests with none failed, a clean stop.
- Linux: 23.5 h, memory +3 MB, 16,736 requests with none failed, a clean stop.

## Verification

- **Full gate** `scripts\Test-v1.1.0.0-Logic.ps1 -RunBuild -RunLong`: 407 / 407 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.9.3 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 342 / 342. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **T-2 and D-12:** the documentation checks pass: one What's new in the README, current milestones on the site, the Linux
  page describing today's support, every listed Linux command present in the service, and v1.1.0.0 named the accepted
  baseline in the README, site, roadmap, checklist and publishing guide.
- **Q-10:** the 24-hour runs' summaries (`C:\GameServers\_Soak\v1.0.7.5\windows` and `linux`) both read PASSED.

## For you

1. **The join problem:** waiting on your double-click test (stop the server in MystTiq, close MystTiq, double-click
   PalServer.exe, join). It tells me whether the wrong character comes from MystTiq's launch or from the game and
   account side.
2. **Live checks only you can do:**
   - run `/mysttiq-backup` from your Discord bot;
   - leave the level-gap rule on for a week.
3. **Push:** v1.0.7.0 to v1.1.0.0 are committed locally and checkpointed, not pushed. Say the word and I'll push and tag
   them; v1.1.0.0 would be the release to publish.
