# v0.4.9.1 Build and Test Plan

Run `scripts/Test-CurrentRelease.ps1`. Promotion requires strict validation, all logic checks, Windows and Linux headless/desktop builds, and Windows runtime smoke to complete without failures.

The regression focus is the RCON Boundary assertion: descriptive UI text may mention sockets, but the desktop must not import `System.Net.Sockets` or construct `TcpClient`/`Socket`; those remain owned by the Core RCON service.
