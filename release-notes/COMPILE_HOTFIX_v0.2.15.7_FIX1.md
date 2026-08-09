# v0.2.15.7 FIX1 — Compile Hotfix

## Trigger
The first v0.2.15.7 Windows compile passed release validation but failed the C# build with CS0103 in `ModService.cs`. The same compile also reported CS8618 and CS0169 in `ModVerificationService.cs`.

## Root cause
During the Unified Runtime State constructor migration, the two-argument `ModService` constructor kept the old identifier `ue4ssRuntimeResolver` even though its declared parameter is `ue4ssResolver`. `GenericModVerifier` also retained an unused `RuntimeStateService` field after verification was migrated to `ModVerificationContext.RuntimeState`.

## Fix
- Chain the two-argument `ModService` constructor through `ue4ssResolver`.
- Remove the unused nested verifier field.
- Add a v0.2.15.7 logic-harness regression check for the constructor chain.

## Validation
Run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.7-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected: zero C# build errors and no CS8618/CS0169 warnings from `GenericModVerifier`.
