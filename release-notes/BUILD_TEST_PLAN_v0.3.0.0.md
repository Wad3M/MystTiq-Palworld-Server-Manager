# Build / Test Plan — v0.3.0.0

## Windows build/regression gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

This must preserve the v0.2.16.4 Windows behavior while also compiling the new shared core and headless host.

## Publish Linux headless host

```powershell
.\Build.ps1 LinuxHeadless
```

Expected outputs:

```text
artifacts\publish\linux-x64\
artifacts\MystTiqHeadless-v0.3.0.0-linux-x64.tar.gz
artifacts\MystTiqHeadless-v0.3.0.0-linux-x64.tar.gz.sha256.txt
```

## Copy to Linux reference host

Example:

```powershell
scp .\artifacts\MystTiqHeadless-v0.3.0.0-linux-x64.tar.gz <linux-user>@<linux-host>:/home/<linux-user>/
```

On Linux:

```bash
mkdir -p ~/mysttiq-v0.3.0.0
cd ~/mysttiq-v0.3.0.0
tar -xzf ~/MystTiqHeadless-v0.3.0.0-linux-x64.tar.gz
chmod +x ./mysttiq-server
```

## Headless acceptance

With PalServer stopped:

```bash
./mysttiq-server probe
./mysttiq-server install-plan
./mysttiq-server probe --json
```

Expected:

- Ubuntu/Linux is detected
- `/opt/mysttiq/palserver` is the default server root
- `/opt/mysttiq/steamcmd/steamcmd.sh` is the default SteamCMD path
- installed server/SteamCMD files are reported
- install plan contains `+@sSteamCmdForcePlatformType linux`
- install plan contains App `2394010`
- no install/update action is executed

Then start the already-validated vanilla PalServer manually and run:

```bash
./mysttiq-server status
```

Expected:

- `PalServer-Linux-Shipping` is detected
- UDP `8211` appears in guarded ports

Stop PalServer manually after the observation test.

## Windows regression

Run the normal Windows runtime smoke test against the v0.2.16.4 behavior: server lifecycle, MOD evidence/health, backup, World Inspector live-save safety, and WORLD PULSE.
