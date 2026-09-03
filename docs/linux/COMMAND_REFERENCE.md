# MystTiq Linux Headless Command Reference

Current release line: **v0.3.0.7**

Validated reference: **Ubuntu Server 24.04.4 LTS x86_64**, observed kernel **6.8.0-137-generic**.

Installed service binary:

```text
/opt/mysttiq/bin/mysttiq-server
```

An extracted build can be invoked with `./mysttiq-server`.

## Complete command list

| Command | Purpose |
|---|---|
| `--help` | Display built-in help |
| `probe` | Platform/distro/path/process/port probe |
| `status` | PalServer lifecycle/readiness |
| `start` | Start PalServer and verify UDP 8211 |
| `stop` | Graceful SIGTERM-first shutdown |
| `restart` | Safe restart with readiness verification |
| `install-plan` | Show SteamCMD plan without executing it |
| `service-status` | Show systemd state |
| `service-install` | Install/enable MystTiq under systemd |
| `service-uninstall` | Stop/disable/remove the systemd unit |
| `service-run` | Long-running systemd supervisor |
| `config-show` | Print effective configuration |
| `config-validate` | Validate configuration |
| `config-write-default` | Write default configuration |
| `config-migrate` | Migrate older supported config |
| `api-token-create` | Create protected bearer token |
| `api-tls-create` | Create TLS server certificate |
| `api-remote-enable` | Enable authenticated + TLS remote API |
| `api-remote-disable` | Restore loopback-only API |
| `api-run` | Run API interactively |
| `production-doctor` | Production checks with evidence/recommendations |

## Probe and Production Doctor

```bash
./mysttiq-server probe
./mysttiq-server probe --json

./mysttiq-server production-doctor
./mysttiq-server production-doctor --json
```

Doctor checks configuration, PalServer/SteamCMD paths, backup root, systemd, PalServer readiness, disk reserve and management API security.

## Lifecycle

```bash
./mysttiq-server status
./mysttiq-server status --json
./mysttiq-server start
./mysttiq-server stop
./mysttiq-server restart
```

## SteamCMD plan

```bash
./mysttiq-server install-plan
```

Informational only; it does not execute installation/update.

## systemd

```bash
./mysttiq-server service-status

sudo ./mysttiq-server service-install \
  --service-user "$USER" \
  --start-now

sudo ./mysttiq-server service-uninstall
```

`service-run` is normally launched by systemd.

Useful diagnostics:

```bash
systemctl is-enabled mysttiq-palworld
systemctl is-active mysttiq-palworld
systemctl status mysttiq-palworld --no-pager -l
systemctl cat mysttiq-palworld
journalctl -u mysttiq-palworld -b --no-pager
```

## Configuration

Default:

```text
/etc/mysttiq/mysttiq.json
```

```bash
./mysttiq-server config-show
./mysttiq-server config-validate
sudo ./mysttiq-server config-write-default
sudo ./mysttiq-server config-write-default --overwrite
sudo ./mysttiq-server config-migrate
```

Alternate config:

```bash
./mysttiq-server config-show --config /path/to/mysttiq.json
```

## Management API

```bash
./mysttiq-server api-run
```

Safe default:

```text
http://127.0.0.1:8213
```

Endpoints:

```text
GET  /healthz
GET  /api/v1/status
GET  /api/v1/service
GET  /api/v1/config
POST /api/v1/server/start
POST /api/v1/server/stop
POST /api/v1/server/restart
```

## Token and TLS

```bash
sudo ./mysttiq-server api-token-create \
  --token-file /etc/mysttiq/secrets/api-token
```

```bash
sudo ./mysttiq-server api-tls-create \
  --bind-address 192.168.1.248 \
  --dns-name mystiqlinux \
  --certificate-file /etc/mysttiq/certs/mysttiq.pfx \
  --certificate-password-file /etc/mysttiq/secrets/certificate-password
```

## Secure remote API

Recommended:

```bash
bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

Return to loopback-only:

```bash
bash ./scripts/Disable-MystTiqRemoteApi.sh
```

Enrollment backs up/migrates config, provisions/reuses token/TLS, normalizes protected permissions, installs/restarts the service and verifies HTTPS authentication. MystTiq does not silently alter firewall rules.

## First-run setup

```bash
bash ./scripts/Install-MystTiqLinux.sh
```

Prepares MystTiq directories, preserves existing config, creates/migrates/validates config and installs systemd.

## Upgrade

```bash
bash ./scripts/Upgrade-MystTiqLinux.sh
```

Creates a pre-upgrade config backup and preserves secrets/TLS, PalServer, saves, backups and runtime data.

## Production readiness and acceptance

```bash
bash ./scripts/Test-v0.3.0.7-ProductionReadiness.sh
bash ./scripts/Test-v0.3.0.7-LinuxAcceptance.sh --extended
```

Reports:

```text
~/mysttiq-test-results/v0.3.0.7/
```

From Windows:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
.\scripts\Test-MystTiqRemoteApi.ps1
```

## Common paths

```text
MystTiq binary        /opt/mysttiq/bin/mysttiq-server
MystTiq config        /etc/mysttiq/mysttiq.json
API token             /etc/mysttiq/secrets/api-token
TLS certificate       /etc/mysttiq/certs/mysttiq.pfx
TLS password          /etc/mysttiq/secrets/certificate-password
PalServer root        /opt/mysttiq/palserver
SteamCMD              /opt/mysttiq/steamcmd/steamcmd.sh
Backup root           /opt/mysttiq/backups
Runtime root          /opt/mysttiq/runtime
PalServer logs        /opt/mysttiq/palserver/Pal/Saved/Logs
PalServer config      /opt/mysttiq/palserver/Pal/Saved/Config/LinuxServer
Save root             /opt/mysttiq/palserver/Pal/Saved/SaveGames
systemd unit          /etc/systemd/system/mysttiq-palworld.service
```

## Native diagnostics

```bash
ps aux | grep -i PalServer
pgrep -af PalServer
ss -lunp | grep 8211
ss -lntp | grep 8213
journalctl -u mysttiq-palworld -b -n 100 --no-pager
df -h /opt/mysttiq
```

## Security expectations

- non-root service account
- `/etc/mysttiq/secrets` mode 0700
- individual secret files mode 0600
- remote API requires bearer authentication + TLS
- do not publish port 8213 to the internet without an intentional security design
- do not store SSH/API passwords or tokens in source control
