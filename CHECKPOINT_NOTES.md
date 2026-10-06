<!-- MystTiq v1.0.3.0: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.3.0 Checkpoint: Packaging and Read-only Access

The second milestone of your roadmap: P-1 Docker image, W-1 read-only browser view, X-1 Xbox player discovery.

## P-1: a Docker image — Done

- The headless service as a Linux container image (`deploy/docker`), labelled with the MystTiq version.
- It runs as its own user, not root, and keeps everything in `/data`: configuration, server files, backups, the API
  token and the TLS certificate.
- On the first start it creates its configuration, a token and a certificate, and serves the API on port 8213 over TLS
  with that token.
- Proven on your Docker Desktop:
  - MystTiq installed the Linux Palworld server inside the container;
  - it ran your clone's world there (REST answering, Day 173 11:02 read from the save);
  - it stopped cleanly.
- Use a Docker volume for `/data`. With a Windows folder mounted instead, the server exited before it was ready.

## W-1: a read-only browser view — Built

- Open `/web` on your MystTiq address (for example `https://your-host:8213/web`) and sign in with a MystTiq account. It
  shows each server's status, who's online (no IP addresses) and the latest backups, refreshed every 30 seconds.
- It can't change anything. All 114 change routes are refused for a browser session, whatever the account may do in
  the desktop. Roles still decide what it can read.
- **Security** in the desktop shows the address.
- Proven on the clone through an isolated service: signed in, the page showed the clone, and a Start sent from the
  page was refused.
- **Owed:** a session from another computer over TLS. The browser rejects MystTiq's self-signed certificate, and I
  don't install certificates into Windows.

## X-1: Xbox discovery — Blocked (needs you)

The script (`scripts\Discover-v1.0.3.0-XboxPlayer.ps1`) and the procedure
(`docs\architecture\v1.0.3.0-xbox-player-discovery.md`) are ready. They need your Xbox account in the clone's world.

## Fixed on the way

A brand-new SteamCMD failed its first server install with "Missing configuration". MystTiq now runs it once more,
which works. Found installing the server into the container.

## Verification

- **Full gate** `scripts\Test-v1.0.3.0-Logic.ps1 -RunBuild`: 300 / 300 passed. It includes the browser view smoke and the Docker image check. The 5 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.2.0 check is carried, and the frozen v1.0.2.0 gate passes on its own checkpoint.
- **Static gate:** 248 / 248. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Browser view smoke** (isolated service, authentication on):
  - the page has no data and a strict content policy;
  - browser sign-in reads status, players and backups;
  - all 114 change routes are refused for the browser session;
  - the same account signed in the desktop's way still writes;
  - a Viewer can't read the account list;
  - sign-out ends the session.
- **Docker** (`Test-v1.0.3.0-Docker.ps1 -RunServer`, image `mysttiq-headless:1.0.3.0`), 11 / 11:
  - version label, Linux, remote-secured;
  - paths under `/data`, non-root user;
  - 401 without the token, status with it;
  - SteamCMD installed the server (the first try failed with "Missing configuration" and the new retry worked);
  - the clone's world ran (REST answering);
  - clean stop.
- **Live W-1 on your clone** (isolated service, a test account, loopback):
  - signed in, the page showed the clone (Stopped, 1 backup, Verified);
  - a Start sent from the page's own session got 403 read-only, while reads got 200.
- **LogicHarness:** the SteamCMD "Missing configuration" rule. **ArtworkHarness:** 794 checks pass, including the browser view card on Security.
- Your own MystTiq (v1.0.0.0, started 18:36) was running during the gate; nothing of it was touched.

## For you

- **X-1:** join the clone from Xbox and run the discovery script (about 5 minutes).
- **W-1:** open the browser view from another computer over TLS (accept the certificate warning once), sign in, and
  tell me it worked.
- **Still from v1.0.2.0:** R-2, a Discord or email channel and a test send; R-3, the Linux VM switched on.
- **Pushed and tagged** with your go-ahead (push through v1.0.5.0).
- **Next:** v1.0.4.0, guarded save edits (remove and add an item first; adding and removing a Pal is the larger part).
