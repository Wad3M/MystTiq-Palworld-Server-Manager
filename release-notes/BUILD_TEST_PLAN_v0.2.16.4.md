# Build / Test Plan — v0.2.16.4

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.4-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance

1. Verify Server Setup still detects an existing SteamCMD installation.
2. Run SteamCMD install/repair and confirm extraction/self-update succeeds.
3. Install/repair Palworld Dedicated Server into the configured server root.
4. Confirm the validated-install attempt still uses App ID 2394010 with `validate`.
5. Confirm fallback install without `validate` still works when needed.
6. Confirm default Steam library recovery still copies PalServer into the configured server root when SteamCMD ignores `force_install_dir`.
7. With the server stopped, run Server Update and confirm stdout/stderr/status reporting still works.
8. Cancel a running update and confirm the SteamCMD process tree is terminated.
9. Re-test Start/Stop/Restart, MOD runtime evidence, Operational Health, World Inspector live-save safety, Backup, and WORLD PULSE.
10. Confirm README/release docs describe Windows support accurately and do not claim Linux is released.
