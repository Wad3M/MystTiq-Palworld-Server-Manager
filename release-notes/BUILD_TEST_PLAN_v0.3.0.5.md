# Build / Test Plan — v0.3.0.5

## Windows gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.5-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

## One-time SSH trust acceptance

```powershell
.\scripts\Initialize-MystTiqLinuxSSH.ps1
```

Expected:

- dedicated Ed25519 identity created or reused
- only the public key is installed on Linux
- final key-only authentication test passes

## Passwordless deployment acceptance

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Expected:

- dedicated-key preflight passes
- no Linux account password prompts
- Linux headless archive builds/publishes
- local SHA-256 is calculated
- SSH remote folder preparation succeeds
- SCP succeeds with the same identity
- remote SHA-256 matches
- extraction succeeds
- automated Linux acceptance reports zero FAIL entries

Password fallback must occur only when explicitly requested with `-AllowPasswordFallback`.
