# Linux Service & Automatic Recovery Architecture — v0.3.0.2

## Ownership model

systemd supervises the long-running MystTiq headless process.

MystTiq supervises PalServer.

```text
systemd
  └── mysttiq-server service-run
       └── LinuxHeadlessSupervisor
            └── LinuxServerLifecycleService
                 └── PalServer-Linux-Shipping
```

This preserves one lifecycle policy for manual CLI operation and service operation.

## Installation

`service-install` requires root only for installation tasks. It:

1. copies the current self-contained host to `/opt/mysttiq/bin/mysttiq-server`
2. sets Linux executable permissions
3. writes `/etc/systemd/system/mysttiq-palworld.service`
4. runs `systemctl daemon-reload`
5. enables the unit
6. optionally starts immediately with `--start-now`

The service itself runs under the selected non-root account.

## Runtime supervision

`service-run` is long-lived. It adopts an already-running PalServer or starts one through the established lifecycle service.

Unexpected PalServer disappearance is treated as crash evidence. Recovery is bounded by:

- poll interval
- recovery backoff
- maximum recovery attempts
- recovery window

If the recovery budget is exhausted, the headless supervisor exits non-zero. systemd can then apply its independent `Restart=on-failure` safety net.

## Shutdown

systemd sends SIGTERM to MystTiq. The host converts that into cancellation and invokes the established graceful PalServer stop path before exiting.

## Logging

- MystTiq supervisor/service output: systemd journal (`journalctl -u mysttiq-palworld`)
- detached PalServer console: `/opt/mysttiq/runtime/palserver-console.log`
- lifecycle evidence: `/opt/mysttiq/runtime/lifecycle-state.json`

## Windows boundary

No Windows WPF lifecycle behavior is changed in v0.3.0.2. Useful service/supervision discoveries are recorded for the v0.4 Windows background-service line.
