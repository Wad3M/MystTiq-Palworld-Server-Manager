# Build / Test Plan — v0.3.0.2

## Windows build/regression gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

## Copy to the reference Linux VM

```powershell
C:\Windows\System32\OpenSSH\scp.exe `
    .\artifacts\MystTiqHeadless-v0.3.0.2-linux-x64.tar.gz `
    mystroth@192.168.1.247:/home/mystroth/
```

On Linux:

```bash
mkdir -p ~/mysttiq-builds/v0.3.0.2
tar -xzf ~/MystTiqHeadless-v0.3.0.2-linux-x64.tar.gz -C ~/mysttiq-builds/v0.3.0.2
cd ~/mysttiq-builds/v0.3.0.2
chmod +x mysttiq-server
./mysttiq-server probe
```

## systemd acceptance

Ensure any manually launched PalServer is stopped first.

```bash
cd ~/mysttiq-builds/v0.3.0.2

./mysttiq-server service-status
sudo ./mysttiq-server service-install --service-user mystroth
./mysttiq-server service-status

sudo systemctl start mysttiq-palworld
sleep 10
./mysttiq-server service-status
./mysttiq-server status

systemctl is-enabled mysttiq-palworld
systemctl is-active mysttiq-palworld
journalctl -u mysttiq-palworld -n 80 --no-pager
```

Expected:
- installed = true
- enabled = true
- systemd service becomes active
- PalServer becomes Running / Ready
- UDP 8211 is detected

## Automatic recovery test

On the disposable Hyper-V VM only:

```bash
pid=$(pgrep -f 'PalServer-Linux-Shipping' | head -n1)
echo "Killing PalServer PID $pid to test recovery"
kill -9 "$pid"

sleep 20

./mysttiq-server status
pgrep -af PalServer
journalctl -u mysttiq-palworld -n 120 --no-pager
```

Expected: MystTiq detects the unexpected disappearance and starts a replacement PalServer process.

## Graceful service stop

```bash
sudo systemctl stop mysttiq-palworld
sleep 5

./mysttiq-server service-status
pgrep -af PalServer || true
cat /opt/mysttiq/runtime/lifecycle-state.json
```

Expected: systemd service is inactive and PalServer is stopped through the MystTiq graceful-stop policy.

## Reboot/boot-start test

Because the unit is enabled:

```bash
sudo reboot
```

After reconnecting:

```bash
systemctl is-enabled mysttiq-palworld
systemctl is-active mysttiq-palworld
./mysttiq-server service-status
pgrep -af PalServer
```

## Uninstall test

After boot-start validation:

```bash
sudo ~/mysttiq-builds/v0.3.0.2/mysttiq-server service-uninstall
systemctl status mysttiq-palworld --no-pager || true
test ! -f /etc/systemd/system/mysttiq-palworld.service
```

Reinstall before promotion if the test VM should retain the service.
