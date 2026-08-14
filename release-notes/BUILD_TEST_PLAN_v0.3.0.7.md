# Build / Test Plan — v0.3.0.7

## Windows gate
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.7-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

## Upgrade-path acceptance
```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Then, if the deployment runner did not install the current build:
```bash
cd ~/mysttiq-builds/v0.3.0.7
bash ./scripts/Upgrade-MystTiqLinux.sh
bash ./scripts/Test-v0.3.0.7-ProductionReadiness.sh
```

## Clean-install acceptance
After upgrade-path acceptance passes, use a clean/disposable Ubuntu 24.04.4 LTS VM and run:
```bash
bash ./scripts/Install-MystTiqLinux.sh
bash ./scripts/Test-v0.3.0.7-ProductionReadiness.sh
```

Promotion requires zero build/logic failures and successful upgrade-path plus clean-install production-readiness evidence.
