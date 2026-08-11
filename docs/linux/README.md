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
