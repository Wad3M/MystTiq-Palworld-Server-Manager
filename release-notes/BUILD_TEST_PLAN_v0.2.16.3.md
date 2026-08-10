# Build / Test Plan — v0.2.16.3

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance

1. Open Server Setup and confirm READY/MISSING/DISABLED/OPTIONAL badges are visibly smaller and centered.
2. Click `CHECK FOR UPDATES` with Internet access.
3. With v0.2.16.3 installed while public v0.2.15.17 is latest, confirm MystTiq reports `DEVELOPMENT BUILD` / newer than public release.
4. Confirm SteamCMD, Palworld Dedicated Server, UE4SS, and Workshop results still appear.
5. Open Update Center → Check All and confirm the MystTiq row shows installed and latest public versions.
6. Confirm a DEVELOPMENT BUILD row offers the GitHub release source rather than an install/update action.
7. Test version parsing with representative values such as 0.2.16.10 > 0.2.16.9.
8. Disconnect Internet temporarily and confirm MystTiq release check becomes CHECK FAILED without crashing the rest of the update workflow.
9. Confirm the official GitHub Releases page opens from the MystTiq Update Center source/update action.
10. Re-test Start/Stop/Restart, MOD runtime evidence, Operational Health, WORLD PULSE, World Inspector live-save safety, and Backup.
