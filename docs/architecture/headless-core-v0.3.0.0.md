# Headless Core Architecture — v0.3.0.0

## Objective

MystTiq's Linux implementation must be able to operate without a graphical desktop. v0.3 therefore introduces a headless-first architecture instead of attempting to port WPF to Linux.

## Project boundary

```text
src/
├── PalworldManager/          Windows WPF application (validated v0.2.16.4 behavior)
├── MystTiq.Core/             platform-neutral / headless-capable services
└── MystTiq.HeadlessHost/     no-GUI command-line host
```

`MystTiq.Core` and `MystTiq.HeadlessHost` target plain `net10.0`, not `net10.0-windows`, and do not use WPF.

## v0.3.0.0 Linux services

```text
ServerRuntimeConfiguration
        │
        ├── ServerPlatformProfile.Linux
        ├── LinuxServerPathProfile
        ├── LinuxServerDistributionPlatformService
        ├── LinuxDistributionService
        └── LinuxServerSessionInspector
                    │
                    ├── /proc/<pid>/stat
                    ├── /proc/<pid>/exe
                    ├── /proc/<pid>/maps
                    └── /proc/net/{tcp,tcp6,udp,udp6}
```

## Headless host safety boundary

v0.3.0.0 is observational by design.

Supported commands:

- `probe`
- `status`
- `install-plan`

The host does not yet:

- start PalServer
- stop/kill PalServer
- execute SteamCMD installation/update commands
- edit server configuration
- install a systemd service
- modify MOD state

Those responsibilities are introduced only after Linux observation/path/distribution behavior is proven on the reference environment.

## Windows protection rule

The WPF project remains intact as the frozen Windows regression implementation. The new headless core is introduced alongside it rather than rewriting validated Windows lifecycle behavior during the first Linux phase.

As v0.3 progresses, reusable services can migrate into `MystTiq.Core` when that migration can be demonstrated behavior-neutral on Windows.

## Linux SteamCMD finding

The validated Ubuntu environment required:

```text
+@sSteamCmdForcePlatformType linux
```

before `app_update 2394010 validate`. The Linux distribution service owns this requirement so it cannot be lost in higher-level lifecycle code.

## Future phases

1. Linux server lifecycle/process operations
2. executable SteamCMD install/update workflow
3. headless configuration and persistence
4. systemd service integration
5. shared API/remote-management boundary
6. Linux world/backup/MOD parity
7. packaging/hardening

Windows headless/service/minimized-resource work is tracked separately for v0.4 in `docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md`.
