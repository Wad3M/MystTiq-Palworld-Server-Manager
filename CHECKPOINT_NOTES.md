<!-- MystTiq v1.0.2.0: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.2.0 Checkpoint: Unattended Reliability

The first milestone of your new roadmap: R-1 frozen-server watchdog, R-2 alert delivery proof, R-3 the Linux service
under systemd.

## R-1: a frozen server is restarted — Done

A server that hung but kept running looked fine to MystTiq, because its game port stayed open. Now:
- MystTiq asks each running server's REST API about every 30 seconds whether it still answers.
- A server that has answered and then stays silent for 3 minutes is restarted. You get "server stopped responding,
  restarting", then "server is back up", and an Activity entry.
- It never touches a healthy server, or one with the REST API switched off.
- The restart counts toward the same limit as crash restarts. When that limit is used up, MystTiq tells you once and
  leaves the server alone.
- The time is `lifecycle.unresponsiveRestartSeconds` in `mysttiq.json` (0 switches it off). Your config doesn't set it,
  so it's 3 minutes.

## R-2: proof that alerts arrive — Built (needs you)

- Every send to Discord, email or a webhook is now recorded with its result.
- **Alert Center > Delivery** shows each channel as Delivered, Failing or Not proven (nothing delivered in 7 days),
  with the latest sends.
- The Dashboard warns when a switched-on channel is failing or not proven.
- **Owed:** none of your servers has a Discord or email channel set up, so there's nothing real to send to yet.

## R-3: the Linux service under systemd — Built (needs the VM)

- The unit already restarts on failure, starts at boot and stops cleanly.
- `scripts\Test-v1.0.2.0-LinuxSystemd.ps1` now proves that on the Linux VM, using its own test unit: install, crash
  (SIGKILL), reboot, stop and clean up. It checks your real unit is unchanged.
- The gate runs it (without the reboot) whenever the VM answers. Today it didn't (192.168.1.122).

## Verification

- **Full gate** `scripts\Test-v1.0.2.0-Logic.ps1 -RunBuild`: 293 / 293 passed. The 5 Linux VM checks (R-3's systemd check included) were skipped because 192.168.1.122 could not be reached. Every v1.0.1.0 check is carried, and the frozen v1.0.1.0 gate passes on its own checkpoint.
- **Static gate:** 243 / 243. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **LogicHarness**, six new scenarios:
  - the watchdog's rules (healthy never frozen; never-answered, REST off and not ready never judged; frozen after exactly the limit; a new process starts clean; 0 is off);
  - the recovery loop: a frozen server restarted once with FrozenDetected then RecoverySucceeded, a give-up said once and left alone, a replaced process announced;
  - REST off: never restarted;
  - the probe against real sockets (a 401 counts as an answer; silence doesn't), the alert text and the mute rule;
  - the delivery states (Delivered, Failing, Not proven, Off);
  - real sends to an answering endpoint and a failing one, each recorded, kept across a restart, capped at 500.
- **ArtworkHarness:** 793 checks pass, including the Dashboard warning, Open Alert Center landing on the Delivery card, the
  tag colours and German.
- **Live R-1 on your clone** (`second-local`, run by an isolated service on its own port; your live config and main
  server not touched):
  - frozen with NtSuspendProcess at 16:23:07, with a 60 s limit;
  - the service logged "has not answered its REST API for 1 minute(s). Frozen-server restart attempt 1/5";
  - a new process was ready 84 s after the freeze, and the frozen one was gone;
  - the alerts "server stopped responding, restarting (attempt 1 of 5)" and "server is back up" were raised, and the
    Activity log has "Frozen server restarted";
  - no outside channel is set up anywhere on this machine, so nothing was sent out.
- **Live, published v1.0.2.0:** Alert Center shows the new Delivery card (Webhook, Discord and Email all Off, "Switched off.").

## For you

- **R-2:** set up a Discord webhook and/or email in Alert Center, then press **Send test notification**. The Delivery
  card shows whether it arrived. Tell me it did, and R-2 can be marked Done.
- **R-3:** when the Linux VM is on, I can run the systemd test with the reboot (it reboots the VM).
- **Pushed and tagged** with your go-ahead (push through v1.0.5.0).
- **Next:** v1.0.3.0. P-1 Docker (Docker Desktop runs here) and W-1 the read-only browser view I can build and prove on the
  clone. X-1 Xbox discovery needs your Xbox account in the clone's world.
