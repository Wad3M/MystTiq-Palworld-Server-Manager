# Build / Test Plan — v0.2.15.12 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.12-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- 0 validation errors
- 0 validation warnings
- no CS0103 errors for EnumerateProcessTree, GetDescendantProcessIds, or GetGuardedListeningPorts
- all architecture and build harness checks pass

After compile validation, continue the v0.2.15.12 runtime behavior-preservation tests.
