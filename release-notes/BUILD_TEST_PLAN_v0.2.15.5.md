# Build & Runtime Test Plan — v0.2.15.5

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: 0 validation errors, 0 validation warnings, successful portable/installer/checksum generation.

## Runtime

1. Start PalServer and open MOD Library.
2. Confirm cached Workshop title `PalSchema (3625280368)` is preserved after Refresh and Verify & Scan All MODs.
3. For each UE4SS/Lua MOD present/enabled but absent from UE4SS load evidence, expect **Runtime Unverified**, not Healthy.
4. For a UE4SS/Lua MOD with matching load evidence, expect **Healthy**.
5. For an enabled UE4SS MOD outside Active Mods Root, expect **Misconfigured**.
6. Confirm PAK/Workshop MODs can be Healthy without `Starting Lua mod` evidence.
7. Confirm MOD Dashboard Healthy count equals only rows whose health is literally Healthy.
8. Confirm the main Dashboard uses the same Healthy count.
9. Verify enable/disable, migration, install, delete, and ZIP normalization from prior phases still operate correctly.
10. Installer regression: default path is `C:\GameServers\MystTiqPalworldServer`, user may change it, and installed launcher requests elevation.
