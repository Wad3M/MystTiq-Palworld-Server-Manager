# Build / Test Plan — v0.2.15.8

## Compile
From the repository root in PowerShell 7:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.8-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected: 0 validation errors, 0 validation warnings, successful win-x64 build/portable/installer/checksums, and zero logic-harness failures.

## Runtime acceptance
1. Start PalServer normally with the current enabled MOD set.
2. Do not run Verify All immediately; allow runtime evidence to arrive naturally.
3. Confirm the MOD Library transitions UE4SS/Lua rows to **Loaded**.
4. Confirm Dashboard rows automatically synchronize when runtime evidence changes; no manual timing-dependent Verify All should be required.
5. Specifically verify **AntiDupe** and **PalImportFilter**. If Library says **Loaded**, Dashboard must become **Healthy** / Runtime **Loaded**, not remain **Runtime Unverified**.
6. Run **VERIFY & SCAN ALL MODS**. Library, Dashboard, verification result, and exported report must agree.
7. Export TXT/JSON verification reports and confirm the evidence text identifies `Unified runtime session` or `Unified inventory state`; when available it also includes the matched runtime alias.
8. Leave PalServer running for at least 5 minutes and perform multiple Library refreshes. Positive current-session state must remain stable.
9. Stop PalServer. Runtime state must clear. Start a new session and confirm evidence is reacquired rather than inherited.
10. Confirm **REFRESH INFO** performs local metadata/runtime refresh only and **SEARCH ONLINE** remains the only browser-search action.

## Evidence-pattern regression
The logic harness statically confirms multiple positive UE4SS patterns are routed through `ModRuntimeEvidenceEngine`, RuntimeStateService consumes the shared extractor, and verification consumes `RuntimeEvidenceAssessment` rather than implementing another independent UE4SS loaded decision.
