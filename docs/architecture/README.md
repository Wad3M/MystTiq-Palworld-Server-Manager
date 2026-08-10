# MystTiq Architecture

Current architecture and platform-completion documentation belongs here.

## Current platform state

- [`platform-completion-audit-v0.2.16.4.md`](platform-completion-audit-v0.2.16.4.md)

The v0.2.16 series isolates major Windows backend responsibilities behind explicit contracts for paths, session/process inspection, server lifecycle operations, and SteamCMD distribution/install/update behavior.

The desktop application itself remains WPF/Windows. Linux implementation begins in the v0.3 line; Linux support is not released in v0.2.16.x.
