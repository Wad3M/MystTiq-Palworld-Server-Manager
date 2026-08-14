# Build / Test Plan — v0.3.0.2 FIX2

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

After copying/extracting the rebuilt Linux archive:

```bash
cd ~/mysttiq-builds/v0.3.0.2
sudo ./mysttiq-server service-install --service-user mystroth --start-now
sudo systemd-analyze verify /etc/systemd/system/mysttiq-palworld.service
systemctl show mysttiq-palworld -p StartLimitIntervalUSec -p StartLimitBurst
journalctl -u mysttiq-palworld -n 40 --no-pager
```

Expected: no unknown-key warning, a 300-second start-limit interval, burst 5, active MystTiq service, and PalServer Running / Ready on UDP 8211.
