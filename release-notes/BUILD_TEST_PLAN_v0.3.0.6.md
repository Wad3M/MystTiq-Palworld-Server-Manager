# Build / Test Plan — v0.3.0.6

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

## Passwordless deployment + Linux acceptance

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

The automated Linux acceptance must report zero FAIL entries, including the temporary TLS provisioning and secured remote-config/rollback checks.

## Explicit LAN enrollment

On the disposable Linux VM, from the extracted v0.3.0.6 folder:

```bash
bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

This is intentionally separate from normal deployment because it changes the API from local-only to LAN-accessible.

## Windows LAN test

```powershell
.\scripts\Test-MystTiqRemoteApi.ps1
```

Expected:

- HTTPS health = 200
- unauthenticated `/api/v1/status` = 401
- valid bearer request = 200
- lifecycle JSON returned

If Windows cannot connect and UFW is active, review the firewall separately. MystTiq must not silently add an allow rule.

## Rollback acceptance

On Linux:

```bash
bash ./scripts/Disable-MystTiqRemoteApi.sh
```

Then confirm the service returns healthy on `127.0.0.1:8213`.
