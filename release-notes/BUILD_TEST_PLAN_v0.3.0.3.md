# Build / Test Plan — v0.3.0.3

## Windows build/regression gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson

.\Build.ps1 LinuxHeadless
```

## Copy the Linux build

From Windows PowerShell:

```powershell
C:\Windows\System32\OpenSSH\scp.exe `
    .\artifacts\MystTiqHeadless-v0.3.0.3-linux-x64.tar.gz `
    mystroth@192.168.1.247:/home/mystroth/
```

On Ubuntu:

```bash
mkdir -p ~/mysttiq-builds/v0.3.0.3
rm -rf ~/mysttiq-builds/v0.3.0.3/*

tar -xzf ~/MystTiqHeadless-v0.3.0.3-linux-x64.tar.gz   -C ~/mysttiq-builds/v0.3.0.3

cd ~/mysttiq-builds/v0.3.0.3
chmod +x mysttiq-server
```

## Configuration acceptance

```bash
./mysttiq-server config-show
./mysttiq-server config-validate

sudo ./mysttiq-server config-write-default --overwrite
./mysttiq-server config-validate
cat /etc/mysttiq/mysttiq.json
```

Expected:

- schemaVersion = 1
- API enabled
- bindAddress = 127.0.0.1
- port = 8213
- Linux paths match the tested environment
- launch arguments match the validated PalServer launch

## Standalone local API acceptance

Stop the systemd MystTiq service temporarily if it is already using port 8213:

```bash
sudo systemctl stop mysttiq-palworld
```

Run the API in SSH window 1:

```bash
cd ~/mysttiq-builds/v0.3.0.3
./mysttiq-server api-run
```

From SSH window 2:

```bash
curl -s http://127.0.0.1:8213/healthz | jq
curl -s http://127.0.0.1:8213/api/v1/status | jq
curl -s http://127.0.0.1:8213/api/v1/service | jq
curl -s http://127.0.0.1:8213/api/v1/config | jq
```

Verify it is not listening on every interface:

```bash
ss -lntp | grep 8213
```

Expected bind:

```text
127.0.0.1:8213
```

A non-loopback configuration must fail validation.

## API lifecycle acceptance

With `api-run` active:

```bash
curl -s -X POST http://127.0.0.1:8213/api/v1/server/start | jq
curl -s http://127.0.0.1:8213/api/v1/status | jq

curl -s -X POST http://127.0.0.1:8213/api/v1/server/restart | jq
curl -s http://127.0.0.1:8213/api/v1/status | jq

curl -s -X POST http://127.0.0.1:8213/api/v1/server/stop | jq
curl -s http://127.0.0.1:8213/api/v1/status | jq
```

Expected:

- start reaches Running / Ready
- UDP 8211 remains detected
- restart returns to Running / Ready with a new PalServer PID
- stop leaves no PalServer process

## systemd/config persistence

Reinstall from the v0.3.0.3 build:

```bash
sudo ./mysttiq-server service-install   --service-user mystroth   --start-now

systemctl cat mysttiq-palworld
```

Verify `ExecStart` includes:

```text
service-run --config "/etc/mysttiq/mysttiq.json"
```

Then:

```bash
sleep 10
curl -s http://127.0.0.1:8213/healthz | jq
./mysttiq-server service-status
./mysttiq-server status
journalctl -u mysttiq-palworld -n 80 --no-pager
```

The service must remain Active, PalServer Running / Ready, and the local API healthy.

A reboot retention test is recommended before promotion because systemd must continue using the configured path after boot.
