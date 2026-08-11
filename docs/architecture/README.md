# MystTiq Architecture

Current architecture and platform-completion documentation belongs here.

## Current platform state

- [`headless-core-v0.3.0.0.md`](headless-core-v0.3.0.0.md) — original headless/Linux foundation architecture
- [`headless-lifecycle-v0.3.0.1.md`](headless-lifecycle-v0.3.0.1.md) — Linux headless lifecycle authority and shutdown policy

- [`platform-completion-audit-v0.2.16.4.md`](platform-completion-audit-v0.2.16.4.md)

The v0.2.16 series isolates major Windows backend responsibilities behind explicit contracts for paths, session/process inspection, server lifecycle operations, and SteamCMD distribution/install/update behavior.

The supported desktop application remains WPF/Windows. v0.3.0.0 now adds a separate cross-platform core and headless host for experimental Linux implementation; full Linux production parity is not yet declared.

- [`linux-service-v0.3.0.2.md`](linux-service-v0.3.0.2.md) — systemd supervision and bounded automatic recovery
