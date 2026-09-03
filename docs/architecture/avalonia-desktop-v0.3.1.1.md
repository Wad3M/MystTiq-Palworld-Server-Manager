# v0.3.1.1 — Avalonia Desktop Foundation

`MystTiq.Desktop` is the first executable cross-platform GUI foundation. It is an API client, not the owner of PalServer lifecycle.

Initial scope:

- Avalonia + .NET 10 project
- Windows x64 / Linux x64 build targets
- MVVM shell
- MystTiq dark visual foundation
- local management API connection profile
- `/api/v1/status` client
- connection/server-state presentation

The Windows WPF client remains supported. Remote bearer-token/TLS profile UX is intentionally deferred to the next connection/security phase rather than storing secrets casually in v0.3.1.1.


## v0.3.1.1 connection-profile security

- Bearer tokens are not persisted in connection-profile storage.
- Saved profiles contain only non-secret connection metadata: name, URL and optional TLS SHA-256 certificate pin.
- Without an explicit pin, normal operating-system TLS trust validation remains authoritative.
- A certificate pin is an explicit trust decision for a known endpoint and must match the server certificate SHA-256 exactly.
