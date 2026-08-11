# Headless Lifecycle Architecture — v0.3.0.1

## Objective

Allow the Linux headless host to control PalServer without coupling lifecycle correctness to an interactive shell or GUI.

## Boundary

`IServerLifecycleService` owns high-level lifecycle semantics.

`LinuxServerLifecycleService` implements the Linux behavior using:

- `IServerSessionInspector` for observed PalServer process/session evidence
- `IProcessSignalService` for POSIX process signalling
- `ServerLifecycleStateStore` for short-lived CLI continuity
- `IServerPathProfile` for server/runtime locations

## Startup

MystTiq writes a small launch helper under the runtime root and uses `setsid -f` so PalServer is detached from the SSH terminal. Standard output/error is redirected to `palserver-console.log`.

Success requires both:

1. native PalServer process evidence
2. guarded UDP port 8211

A port timeout does not automatically kill a process that is otherwise alive.

## Shutdown

The shutdown policy is deliberately conservative:

1. persist stop intent
2. send SIGTERM
3. wait for graceful timeout
4. capture remaining managed descendants
5. use SIGKILL only as escalation
6. verify managed PalServer processes disappeared

## Crash evidence

Lifecycle state is persisted between CLI invocations. If a previously Running/Starting server disappears without a MystTiq stop request, `status` may report `Crashed`.

This is operational evidence, not a full crash-root-cause diagnosis.

## Windows boundary

The existing WPF Windows lifecycle remains unchanged. Linux discoveries that can improve Windows service/headless behavior are recorded for v0.4 rather than being forced into the Windows GUI during v0.3.
