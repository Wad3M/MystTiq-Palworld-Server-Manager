# Build / Test Plan — v0.3.0.6 FIX1

## Windows gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.6-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected: zero build errors, zero validation warnings, zero logic-harness failures.

## Automated passwordless Linux gate

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Expected: zero FAIL results.

## Explicit remote enrollment

SSH to the disposable Ubuntu VM:

```bash
cd ~/mysttiq-builds/v0.3.0.6

bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

The runner itself must finish with `ENROLLMENT PASS`. Do not manually create tokens/certificates or edit the JSON to make the test pass.

## Windows LAN acceptance

```powershell
.\scripts\Test-MystTiqRemoteApi.ps1
```

Expected: final `REMOTE API PASS` with HTTPS, 401 and bearer-auth 200 all passing.

## Rollback

```bash
bash ./scripts/Disable-MystTiqRemoteApi.sh
```

Then confirm normal local acceptance remains healthy.
