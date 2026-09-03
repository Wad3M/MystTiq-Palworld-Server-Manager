# v0.4.8.0 Build/Test Plan

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.8.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Runtime acceptance: load Configuration; verify Simple/Advanced share the same values; search/category filter; generate server name; apply a QoL preset; verify dirty/validation state; export; change a value; import; save; reload and verify persistence/rollback behavior. Repeat against a remote/LAN profile to confirm import/export stays local and Save remains API-routed.
