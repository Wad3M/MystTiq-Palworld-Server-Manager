# Linux Tested Environment

This document identifies the reference environment used to validate MystTiq's initial Linux/headless implementation.

## Validated guest operating system

- **Distribution:** Ubuntu Server 24.04.4 LTS
- **Ubuntu series:** Noble Numbat / 24.04 LTS
- **Architecture:** x86_64 / amd64
- **Kernel observed during validation:** `6.8.0-137-generic`
- **Desktop environment:** none; headless server installation
- **Remote administration:** OpenSSH

## Virtualization used during development

- Generation 2 Hyper-V virtual machine
- 4 vCPU
- Dynamic Memory enabled
- 3 GB startup / 2 GB minimum / 10 GB maximum during initial validation
- 80 GB dynamically expanding VHDX
- External virtual switch

Virtualization is a development/test detail, not a Linux runtime requirement.

## SteamCMD / Palworld validation

- Valve Linux SteamCMD: `/opt/mysttiq/steamcmd/steamcmd.sh`
- Palworld server root: `/opt/mysttiq/palserver`
- Steam App ID: `2394010`
- Linux depot observed: `2394012`
- launch script: `/opt/mysttiq/palserver/PalServer.sh`
- native server process: `/opt/mysttiq/palserver/Pal/Binaries/Linux/PalServer-Linux-Shipping`
- game listener validated: UDP `8211`

### Required SteamCMD platform override

The reference environment returned:

```text
ERROR! Failed to install app '2394010' (Missing configuration)
```

when the normal anonymous `app_update 2394010 validate` command was used without an explicit platform selection.

The installation succeeded when Linux was forced explicitly:

```text
+@sSteamCmdForcePlatformType linux
+force_install_dir /opt/mysttiq/palserver
+login anonymous
+app_update 2394010 validate
+quit
```

`LinuxServerDistributionPlatformService` therefore owns this platform override from v0.3.0.0 onward.

## Validation completed

The reference environment successfully demonstrated:

- SSH/headless access
- SteamCMD installation and self-update
- anonymous Steam connection
- Palworld App metadata reporting Linux support
- Palworld Linux depot availability
- full Palworld Dedicated Server installation
- native `PalServer-Linux-Shipping` process launch
- UDP 8211 listener
- clean Hyper-V checkpoint after vanilla server validation

## Support statement

This is a **tested development reference**, not a declaration that all Ubuntu 24.04 systems or other Linux distributions are fully supported yet. Linux production support will be declared only after the v0.3 parity/hardening gates are complete.

## v0.3.0.1 lifecycle validation target

v0.3.0.1 continues to target the same validated reference environment:

- Ubuntu Server 24.04.4 LTS (Noble)
- x86_64 / amd64
- observed kernel 6.8.0-137-generic
- headless OpenSSH administration
- SteamCMD under `/opt/mysttiq/steamcmd`
- PalServer under `/opt/mysttiq/palserver`
- MystTiq runtime state under `/opt/mysttiq/runtime`
- Palworld Dedicated Server App 2394010 / Linux depot 2394012

The lifecycle acceptance pass for this version must verify headless start, duplicate-start protection, restart, SIGTERM-first stop, stopped-state handling, crash detection, and UDP 8211 readiness.

## v0.3.0.2 service validation target

The systemd/service phase targets the same reference host:

- Ubuntu Server 24.04.4 LTS (Noble)
- x86_64 / amd64
- observed kernel 6.8.0-137-generic
- systemd available as the init/service manager
- service account: `mystroth` on the reference VM
- installed MystTiq service binary: `/opt/mysttiq/bin/mysttiq-server`
- unit: `mysttiq-palworld.service`
- PalServer: `/opt/mysttiq/palserver`
- runtime state/log root: `/opt/mysttiq/runtime`

v0.3.0.2 acceptance must verify install/enable, boot-capable service state, start, journal output, PalServer adoption/startup, automatic crash recovery, graceful service stop, and clean uninstall.
