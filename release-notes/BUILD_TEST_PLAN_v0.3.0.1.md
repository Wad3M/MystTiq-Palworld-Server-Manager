# Build / Test Plan — v0.3.0.1

## Windows build/regression gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.1-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Then publish the Linux host:

```powershell
.\Build.ps1 LinuxHeadless
```

Copy the resulting archive to the validated Ubuntu host and verify its SHA-256 before extraction.

## Linux runtime acceptance

From the extracted `linux-x64` headless host:

```bash
./mysttiq-server probe
./mysttiq-server status
./mysttiq-server start
./mysttiq-server status
./mysttiq-server start
echo $?
./mysttiq-server restart
./mysttiq-server status
./mysttiq-server stop
./mysttiq-server status
```

Expected behavior:

1. `probe` identifies Ubuntu Server 24.04.4 LTS, the correct kernel release, paths, SteamCMD, and runtime root.
2. Initial `status` is Stopped when PalServer is not active.
3. `start` launches PalServer without keeping the SSH command attached.
4. Successful start detects `PalServer-Linux-Shipping` and UDP 8211.
5. A second `start` is blocked and returns exit code 10.
6. `restart` produces a new native PID and returns to Running/Ready.
7. `stop` reports graceful SIGTERM shutdown where possible.
8. `pgrep -af PalServer` shows no server process after successful stop.
9. Repeating `stop` returns exit code 11 without damaging state.
10. `/opt/mysttiq/runtime/palserver-console.log` exists.
11. `/opt/mysttiq/runtime/lifecycle-state.json` exists and is valid JSON.
12. Manual crash test: start PalServer, record PID, kill the native process outside MystTiq, then run `status`; crash evidence should be surfaced and exit code 16 returned.

Do not test SIGKILL escalation by intentionally hanging a production world. Use only the disposable Linux test VM/checkpoint for escalation tests.
