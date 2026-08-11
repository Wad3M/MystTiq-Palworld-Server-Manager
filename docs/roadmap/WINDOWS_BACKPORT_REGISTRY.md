# Windows Backport / Improvement Registry

During every v0.3 Linux implementation phase, useful discoveries are classified so improvements are not lost.

## Classification rule

### SHARED
Implement during v0.3 when the improvement belongs in the platform-neutral core and benefits both Windows and Linux.

### LINUX
Linux-specific behavior remains in the v0.3 Linux implementation.

### WINDOWS-BACKPORT
Useful Windows-specific improvements discovered during Linux development are recorded here for the v0.4 line unless they are required immediately for shared-core correctness.

## Current registry

| Classification | Improvement | Target | Status |
|---|---|---|---|
| SHARED | Headless-first core that does not require a GUI for server-management correctness | v0.3 | In progress |
| SHARED | Explicit platform/distro/path/distribution/session contracts | v0.3 | In progress |
| SHARED | Structured command/status output suitable for automation and remote clients | v0.3 | Started |
| WINDOWS-BACKPORT | Windows service/headless host using the shared core | v0.4 | Planned |
| WINDOWS-BACKPORT | Low-resource minimized WPF mode with slower/paused UI-only polling and rendering | v0.4 | Planned |
| WINDOWS-BACKPORT | UI reconnects to a persistent background/headless core rather than owning server-management state | v0.4 | Planned |
| WINDOWS-BACKPORT | Service-style watchdog/recovery behavior inspired by Linux/systemd supervision | v0.4 | Discovery backlog |
| WINDOWS-BACKPORT | Improved structured log rotation/retention if Linux implementation proves useful | v0.4 | Discovery backlog |

## Workflow

Every v0.3 release review must ask:

1. Did this phase expose a platform-neutral improvement? If yes, mark **SHARED** and implement it in the core when safe.
2. Is the behavior truly Linux-only? If yes, mark **LINUX**.
3. Would the Windows product benefit but the work is not required for Linux correctness? If yes, record **WINDOWS-BACKPORT** for v0.4.

## v0.3.0.1 discoveries

### SHARED

- Stable headless lifecycle exit-code contract for automation/service callers.
- Persisted lifecycle transition evidence independent of any GUI session.
- Separate observed process state from requested lifecycle intent.
- Startup readiness should require both process evidence and service/game-port evidence.

### LINUX

- Detached `setsid` launch behavior for SSH-independent PalServer operation.
- POSIX SIGTERM-first shutdown with SIGKILL escalation.
- `/proc/sys/kernel/osrelease` is the authoritative kernel-release source for the tested host.

### WINDOWS-BACKPORT

Target: v0.4.x

- Windows headless/service host should use the same lifecycle result/exit-code semantics.
- Add graceful-stop-first policy with explicit force escalation rather than coupling shutdown to UI buttons.
- Persist lifecycle state so Windows background management survives UI closure/restart.
- Add service/watchdog crash evidence independent of WPF.
- Reuse process + guarded-port readiness semantics for Windows service startup.
- Capture background PalServer console/session diagnostics without requiring the WPF log surface.

## v0.3.0.2 discoveries

### SHARED

- Separate service-manager supervision from game-server lifecycle ownership.
- Bounded automatic recovery with backoff and a restart-attempt window.
- Long-running headless supervisor can adopt an existing server instead of forcing ownership only at launch.
- Service logs and server-console logs should remain separate diagnostic channels.

### LINUX

- systemd unit installation, enablement, service status, and journal integration.
- SIGTERM from systemd maps to MystTiq's graceful PalServer stop path.
- Install the stable service executable under `/opt/mysttiq/bin` rather than running from a versioned test folder.

### WINDOWS-BACKPORT

Target: v0.4.x

- Windows Service host using the same supervisor/lifecycle ownership split.
- Service automatic-recovery budget/backoff independent of WPF.
- Windows Event Log or structured background-service logging separate from UI logs.
- Service installation should copy a stable executable into an application-owned service location.
- WPF should be able to attach to/adopt an already-running background MystTiq service.
