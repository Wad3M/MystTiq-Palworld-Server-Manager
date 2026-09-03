# v0.3.1.3 Monitoring Architecture

## Authority boundary

The Avalonia desktop does not directly read PalServer logs, process information, `PalWorldSettings.ini`, or Palworld REST credentials.

```text
MystTiq.Desktop
        ↓ authenticated MystTiq API
MystTiq persistent service
        ├─ lifecycle/status
        ├─ bounded log tail
        ├─ process metrics
        └─ loopback Palworld REST /players
```

## Player credential boundary

The Palworld `AdminPassword` remains on the managed server in `PalWorldSettings.ini`. MystTiq may use it internally to authenticate to:

```text
http://127.0.0.1:<RESTAPIPort>/v1/api/players
```

The password is never included in the MystTiq player snapshot.

## Log boundary

One log-tail request returns at most 500 lines and reads no more than 512 KiB from the end of the current Palworld log. The GUI does not request or download the entire log file.

## Metrics

Metrics are sampled from the authoritative PalServer PID reported by the lifecycle service:

- normalized CPU percent derived from process CPU-time deltas
- working-set bytes
- thread count

The first CPU observation establishes a baseline.
