<!-- MystTiq v1.1.0.0: file reviewed for this release (2026-10-12). -->
# MystTiq on Linux

MystTiq's Linux support is **headless-first**: managing the server never depends on a desktop being present. The headless
service (`mysttiq-server`) runs the Palworld server, backups, automation and the API; the Avalonia desktop connects to it
locally or remotely, from Linux or Windows.

For every command see **[COMMAND_REFERENCE.md](COMMAND_REFERENCE.md)**; for the machines it has been tested on, see
**[TESTED_ENVIRONMENT.md](TESTED_ENVIRONMENT.md)**.

## Ways to run the service

| How | When to use it | Notes |
| --- | --- | --- |
| A systemd unit (system-wide) | A dedicated server machine | `mysttiq-server service-unit` prints MystTiq's own unit text; `service-install` installs it (needs root). It starts at boot, restarts after a crash and stops the game server with it. |
| A per-user systemd unit | No root access, or a shared machine | The same unit text without `User=`, installed under `~/.config/systemd/user` and wanted by `default.target`. Tested live without sudo (v1.0.6.0). |
| `mysttiq-server service-run` | A terminal or another supervisor | The service in the foreground, with crash recovery. Ctrl+C stops MystTiq and leaves the game running (v1.0.8.0). |
| The Docker image | Containers | `deploy/docker/Dockerfile`: a non-root `mysttiq` user, everything under one `/data` volume, a health check on `/healthz`. It installs and runs the Palworld server inside the container. Use a Docker volume for `/data`. |

The Linux download (`MystTiqPalworldServer_vX_Linux-x64.zip`) holds the desktop and the service; after extracting,
`chmod +x MystTiq.Desktop headless/mysttiq-server`. `Build.ps1 DeployDesktopLinux` (`scripts/Install-MystTiqDesktopLinux.ps1`)
copies it to a Linux machine over SSH, checks its SHA-256, unpacks it into `~/MystTiq/<version>` with `~/MystTiq/current`
pointing at it, and adds a desktop launcher.

## Remote access

The service listens on loopback until you enable remote access, which takes a bearer token and TLS together
(`api-token-create`, `api-tls-create`, then `api-remote-enable`; `api-remote-disable` returns it to loopback). The steps,
with the enrollment script that does them in one go, are under "Token and TLS" and "Secure remote API" in the
[command reference](COMMAND_REFERENCE.md). The desktop pins the certificate it first sees; named accounts and roles then
decide what each person can do.

## What is and is not covered on Linux

- **Covered and tested live on the reference VM:** the service under systemd (boot, crash, stop), the per-user unit, the
  desktop window, clipboard and tray under XFCE, processor-core pinning and priority, console capture, and the 24-hour idle
  run (v1.0.7.5).
- **Still open** (tracked on the [roadmap](../roadmap/PRODUCT_ROADMAP.md) as E-4): the desktop's file pickers, display
  scaling, and desktop environments other than XFCE. The release workflow publishes the Windows download only until those
  are done.
- Chinese, Japanese and Korean text in the desktop needs a suitable system font such as Noto Sans CJK.

The original v0.3 Linux development plan (detection, path profile, SteamCMD policy, the first systemd unit) is kept in
[history](../history/LINUX_DEVELOPMENT_v0.3.md).
