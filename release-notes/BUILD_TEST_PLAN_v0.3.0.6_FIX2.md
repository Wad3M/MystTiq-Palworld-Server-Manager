# Build / Test Plan — v0.3.0.6 FIX2

Run the Windows gate:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.6-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

The LinuxHeadless output must show these scripts being included:

```text
Test-v0.3.0.6-LinuxAcceptance.sh
Configure-MystTiqRemoteApi.sh
Disable-MystTiqRemoteApi.sh
```

Then deploy:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

On Linux verify:

```bash
cd ~/mysttiq-builds/v0.3.0.6
ls -l ./scripts/Configure-MystTiqRemoteApi.sh
ls -l ./scripts/Disable-MystTiqRemoteApi.sh
```

Only after both files are present should remote enrollment continue.
