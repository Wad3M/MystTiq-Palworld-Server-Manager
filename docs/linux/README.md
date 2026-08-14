# MystTiq Linux Development

v0.3 is the Linux/headless development line.

The Linux implementation is built around a **headless-first** rule: server-management correctness must not depend on a GUI being present. The existing Windows WPF application remains the validated Windows reference while reusable functionality is introduced in `MystTiq.Core` and exercised through `MystTiq.HeadlessHost`.

## v0.3.0.0 scope

The first phase is intentionally non-destructive:

- platform and Linux distribution detection
- Linux path profile
- Linux PalServer process naming/profile
- Linux SteamCMD package and command policy
- Linux procfs process/session inspection
- guarded-port observation
- headless `probe`, `status`, and `install-plan` commands

Starting/stopping PalServer, executing SteamCMD installs/updates, systemd service installation, and remote API control are later v0.3 phases.

## Reference environment

See [`TESTED_ENVIRONMENT.md`](TESTED_ENVIRONMENT.md).

## v0.3.0.1 scope

The Linux headless host now adds lifecycle commands (`status`, `start`, `stop`, `restart`) on top of the validated v0.3.0.0 observation layer.

This remains experimental Linux development. systemd service installation, scheduled supervision, complete configuration parity, backups, MOD parity, and remote API/service hosting are later v0.3 phases.

## v0.3.0.3 scope

The Linux headless host now has persistent JSON configuration plus a Kestrel-based local management API.

The API is intentionally loopback-only in v0.3.0.3. Do not expose port 8213 on the LAN. Remote access/authentication is a later security phase.


## v0.3.0.4 scope

The management API gains authentication/TLS configuration and fail-closed remote-binding rules. The default remains loopback-only with authentication/TLS disabled.

Automated acceptance is now packaged with the Linux build:

```bash
bash ./scripts/Test-v0.3.0.4-LinuxAcceptance.sh
```

Use `--extended` only when lifecycle mutation testing is appropriate on the disposable test VM.


For a full disposable-VM pass that installs the extracted build into systemd and exercises lifecycle mutations:

```bash
bash ./scripts/Test-v0.3.0.4-LinuxAcceptance.sh --install-current --extended
```


## v0.3.0.5 scope

Routine deployment now uses a dedicated SSH key. Run the Windows-side one-time setup:

```powershell
.\scripts\Initialize-MystTiqLinuxSSH.ps1
```

After trust is established:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

should complete normal SSH/SCP work without password prompts.


## v0.3.0.6 scope

Remote API exposure becomes an explicit enrollment operation rather than a manual JSON/certificate task.

On the disposable Linux VM:

```bash
bash ./scripts/Configure-MystTiqRemoteApi.sh --bind 192.168.1.248
```

To return to the safe loopback configuration:

```bash
bash ./scripts/Disable-MystTiqRemoteApi.sh
```

From Windows, validate actual LAN HTTPS/authentication:

```powershell
.\scripts\Test-MystTiqRemoteApi.ps1
```

Firewall rules are never changed implicitly.


## v0.3.0.7 production workflow

Upgrade an existing Linux installation:

```bash
cd ~/mysttiq-builds/v0.3.0.7
bash ./scripts/Upgrade-MystTiqLinux.sh
bash ./scripts/Test-v0.3.0.7-ProductionReadiness.sh
```

For a first-run MystTiq installation on an already prepared Palworld host:

```bash
bash ./scripts/Install-MystTiqLinux.sh
bash ./scripts/Test-v0.3.0.7-ProductionReadiness.sh
```

The helpers preserve existing MystTiq configuration and runtime data. The production-readiness runner captures Doctor output, systemd state, current-boot journal evidence, PalServer readiness and disk reserve into a timestamped report.
