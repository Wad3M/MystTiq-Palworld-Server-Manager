# Build / Test Plan — v0.2.15.10

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.10-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Start PalServer and wait 30-60 seconds.
2. Verify all MODs.
3. AntiDupe and PalImportFilter should report Confirmed Loaded if their exact DLL paths are mapped into the current PalServer process tree.
4. Evidence must identify `PalServer native module table`, PID/session detail, and mapped canonical path.
5. Both `main.dll` mods must resolve independently by full path.
6. Disable one native mod, restart, and verify it is not confirmed from stale/other-mod evidence; then re-enable it.
7. Stop/start PalServer and verify old module evidence is not inherited.
8. Lua mods must retain existing log/session evidence behavior.
9. If module enumeration is unavailable, native mods remain Active / Unverified rather than Failed.
10. Functional activity may still promote Confirmed Loaded to Confirmed Running.
