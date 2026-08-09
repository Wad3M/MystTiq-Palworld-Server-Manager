# Apply Instructions — v0.2.15.3 FIX1

1. Apply the FIX1 changed-files package over **v0.2.15.3** or use the FIX1 full-source package.
2. From the repository root run:
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```
3. Perform `BUILD_TEST_PLAN_v0.2.15.3_FIX1.md`.
4. Fresh installs should default to `C:\GameServers\MystTiqPalworldServer`; existing upgrades intentionally retain their previous selected directory.
5. Verify both the post-install launch and later shortcut launches run with administrative privileges.
6. Continue the original v0.2.15.3 MOD migration tests before baseline promotion.
