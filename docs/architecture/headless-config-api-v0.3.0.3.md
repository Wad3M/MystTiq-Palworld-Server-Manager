# Headless Configuration & Local Management API Architecture — v0.3.0.3

## Configuration

The Linux headless host defaults to:

```text
/etc/mysttiq/mysttiq.json
```

The schema is explicitly versioned. v0.3.0.3 schema version is `1`.

Configuration owns operational values that were previously embedded in CLI/service code:

- server/SteamCMD/backup/runtime paths
- PalServer launch arguments
- startup/stop timeouts
- service polling
- recovery backoff/budget/window
- local API settings

CLI path/time overrides remain available for troubleshooting, but service operation consumes persistent configuration.

## Security boundary

The management API is **loopback-only** in v0.3.0.3.

The validator rejects any API bind address that is not `127.0.0.1`, `::1`, or another IP classified by .NET as loopback. There is no supported LAN/public bind path in this version.

Because the API cannot leave the host in this phase, authentication/token storage is intentionally deferred rather than introducing incomplete security.

## API

Default endpoint:

```text
http://127.0.0.1:8213
```

Endpoints:

- `GET /healthz`
- `GET /api/v1/status`
- `GET /api/v1/service`
- `GET /api/v1/config`
- `POST /api/v1/server/start`
- `POST /api/v1/server/stop`
- `POST /api/v1/server/restart`

Lifecycle mutations share one non-blocking semaphore. A simultaneous lifecycle request receives HTTP 409 instead of racing the active operation.

## systemd integration

`service-run` starts the API before entering the long-running supervisor. Shutdown stops the API and then uses the established graceful PalServer service-stop path.

`service-install` preserves the selected configuration path in `ExecStart`, so custom configuration remains effective after reboot.

## Windows boundary

Windows WPF remains untouched. The v0.4 registry records the configuration/API model as a candidate control plane between a future Windows background service and the desktop UI.
