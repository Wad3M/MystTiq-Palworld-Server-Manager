# Apply Instructions — v0.3.0.2

Apply over official Linux/headless baseline **v0.3.0.1 FIX1**.

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

The Changed Files package includes a deletion notice for the obsolete active v0.3.0.1 logic harness.

systemd installation modifies `/opt/mysttiq/bin` and `/etc/systemd/system`; test it only on the disposable Linux VM until v0.3.0.2 is promoted.
