# MystTiq Architecture

Current architecture and platform-completion documentation belongs here.

## Current platform state

- [`headless-core-v0.3.0.0.md`](headless-core-v0.3.0.0.md) — original headless/Linux foundation architecture
- [`headless-lifecycle-v0.3.0.1.md`](headless-lifecycle-v0.3.0.1.md) — Linux headless lifecycle authority and shutdown policy

- [`platform-completion-audit-v0.2.16.4.md`](platform-completion-audit-v0.2.16.4.md)

The v0.2.16 series isolates major Windows backend responsibilities behind explicit contracts for paths, session/process inspection, server lifecycle operations, and SteamCMD distribution/install/update behavior.

The supported desktop application remains WPF/Windows. v0.3.0.0 now adds a separate cross-platform core and headless host for experimental Linux implementation; full Linux production parity is not yet declared.

- [`linux-service-v0.3.0.2.md`](linux-service-v0.3.0.2.md) — systemd supervision and bounded automatic recovery

- [`headless-config-api-v0.3.0.3.md`](headless-config-api-v0.3.0.3.md) — persistent headless configuration and loopback management API

- [`secure-api-automation-v0.3.0.4.md`](secure-api-automation-v0.3.0.4.md) — API authentication/TLS boundary, config migration and automated Linux acceptance

- [`passwordless-deployment-v0.3.0.5.md`](passwordless-deployment-v0.3.0.5.md) — dedicated SSH trust and passwordless Linux deployment

- [`remote-api-enrollment-v0.3.0.6.md`](remote-api-enrollment-v0.3.0.6.md) — explicit LAN enrollment, certificate provisioning and remote API acceptance

- [`production-readiness-v0.3.0.7.md`](production-readiness-v0.3.0.7.md) — Linux integration, first-run/upgrade automation and production Doctor gate

- [`avalonia-desktop-v0.3.1.md`](avalonia-desktop-v0.3.1.md) — shared Windows/Linux Avalonia client architecture for v0.3.1.x

- [`monitoring-v0.3.1.3.md`](monitoring-v0.3.1.3.md) — player/log/runtime monitoring API boundary

- [`backup-configuration-v0.3.1.4.md`](backup-configuration-v0.3.1.4.md) — backup safety and restricted configuration editing

- [`doctor-diagnostics-v0.3.1.5.md`](doctor-diagnostics-v0.3.1.5.md) — production Doctor API and Avalonia diagnostics UI

- [`server-distribution-v0.3.1.6.md`](server-distribution-v0.3.1.6.md) — service-owned SteamCMD and Palworld server update boundary

- [`world-explorer-v0.3.1.7.md`](world-explorer-v0.3.1.7.md) — read-only server-authoritative world/save inventory boundary

- [`player-guild-explorer-v0.3.1.8.md`](player-guild-explorer-v0.3.1.8.md) — player-save and authoritative guild semantic evidence model

- [`mod-ue4ss-v0.3.1.9.md`](mod-ue4ss-v0.3.1.9.md) — cross-platform MOD/UE4SS state and evidence boundary
