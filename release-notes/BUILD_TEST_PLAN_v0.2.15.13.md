# Build / Test Plan — v0.2.15.13

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.13-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Run with all installed MODs intentionally disabled. Overall Health must not be reduced by the MOD platform.
2. The Dashboard health strip should show `Mods: Disabled` or the disabled count rather than `0 working / N installed`.
3. The MOD Platform card should describe the MODs as intentionally disabled, not `need review`.
4. Enable the normal MOD set and verify confirmed MODs remain Healthy.
5. Leave a valid MOD Active / Unverified; Overall Health must not be reduced solely for lack of positive runtime evidence.
6. Create/observe a genuine enabled-MOD failure or runtime error and confirm Overall Health is reduced.
7. A confirmed conflict or missing dependency on an enabled MOD should reduce Overall Health.
8. Disabled MODs with local issues must remain neutral to server-level health.
9. Verify Overall Health tooltip distinguishes informational MOD state from warning/error state.
10. Re-run start/stop/restart and native module verification to ensure no regression from v0.2.15.12 FIX1.
