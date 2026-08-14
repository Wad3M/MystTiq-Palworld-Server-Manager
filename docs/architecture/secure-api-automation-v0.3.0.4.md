# Secure Management API & Automated Acceptance Architecture — v0.3.0.4

## API exposure policy

The safe default remains `127.0.0.1:8213`.

A non-loopback bind is valid only when **both** conditions are configured:

1. bearer-token authentication enabled
2. TLS enabled

This is a fail-closed configuration rule. The API host independently repeats the same boundary at runtime.

## Secrets

Ordinary operational configuration remains in `/etc/mysttiq/mysttiq.json`. Secrets do not.

Default secret paths:

```text
/etc/mysttiq/secrets/api-token
/etc/mysttiq/secrets/certificate-password
```

`api-token-create` generates at least 32 cryptographically random bytes and writes the token with owner-only Linux file permissions. Bearer-token comparison uses fixed-time byte comparison.

The TLS certificate is expected at `/etc/mysttiq/certs/mysttiq.pfx`. Certificate provisioning/trust policy remains an administrator responsibility in this foundation phase.

`/healthz` remains unauthenticated and deliberately contains no server-management data. `/api/v1/*` requires a bearer token whenever authentication is enabled.

## Configuration migration

Schema v0.3.0.3 (`schemaVersion: 1`) is recognized and migrated in memory to schema version 2. `config-migrate` persists the supported migration. `service-install` also persists a pending supported migration before installing/restarting the service.

## Automated Linux acceptance

The version-matched Linux archive includes:

```text
scripts/Test-v0.3.0.4-LinuxAcceptance.sh
```

It collects raw evidence and produces a consolidated PASS/FAIL/WARN report under:

```text
~/mysttiq-test-results/v0.3.0.4/<UTC timestamp>/
```

Default mode is non-destructive. It checks the reference environment, effective config, systemd unit, restart throttling, API health/listener, PalServer readiness, current-boot journal, unsafe-remote validation and a temporary authenticated loopback API.

`--extended` may exercise lifecycle mutation on the disposable test VM.

The Windows-side `scripts/Deploy-Test-MystTiqLinux.ps1` builds, hashes, copies, extracts and invokes the Linux runner in one workflow.
