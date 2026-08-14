# Secure Remote API Enrollment & TLS Provisioning — v0.3.0.6

## Principle

Remote exposure must remain an explicit administrative action.

MystTiq's default remains:

```text
http://127.0.0.1:8213
```

v0.3.0.6 adds enrollment tooling for a secured LAN listener. The existing fail-closed rule remains authoritative: non-loopback binding requires both bearer authentication and TLS.

## TLS provisioning

`api-tls-create` creates a self-signed PFX suitable for the development/test environment.

Cryptographic properties:

- RSA 3072-bit key
- SHA-256
- TLS Web Server Authentication EKU
- Basic Constraints: non-CA
- Digital Signature + Key Encipherment
- SAN containing the selected literal IP
- SAN containing `localhost`
- optional DNS SAN
- maximum validity 825 days

The random PFX password is written to a separate protected secret file.

Self-signed certificates are intended for the current test environment. Production remote management should eventually support administrator-provided or automatically enrolled trusted certificates.

## Secret ownership

When enrollment is performed through `Configure-MystTiqRemoteApi.sh`, the token, PFX and PFX-password file are:

- owned by the selected systemd service user
- mode `0600`

This closes the gap where root-created secret files could otherwise be unreadable by the non-root MystTiq service.

## Enrollment

Linux:

```bash
bash ./scripts/Configure-MystTiqRemoteApi.sh --bind <LAN-IP>
```

The workflow:

1. explicit confirmation
2. one sudo authorization
3. token creation/reuse
4. certificate/password creation/reuse
5. ownership/permission normalization
6. typed configuration update
7. systemd reinstall/restart
8. unit validation
9. HTTPS health check
10. authenticated API status check

No firewall rule is created automatically.

## Windows-side LAN acceptance

`Test-MystTiqRemoteApi.ps1` uses the dedicated v0.3.0.5 SSH identity to retrieve the token into process memory only, then tests the API over the LAN.

The certificate is self-signed, so acceptance intentionally bypasses certificate-chain trust while still verifying TLS transport and the configured endpoint. Later production hardening should replace this with trusted certificate validation.

## Safe rollback

```bash
bash ./scripts/Disable-MystTiqRemoteApi.sh
```

returns bind/auth/TLS settings to the loopback defaults and restarts the service.


## FIX1 reliability contract

Remote enrollment must not report success merely because individual commands returned.

A successful enrollment now requires verified state at every layer:

```text
secret files
    ↓
ownership / permissions
    ↓
effective configuration readback
    ↓
systemd active
    ↓
exact LAN TCP listener
    ↓
HTTPS health
    ↓
401 without bearer token
    ↓
200 with bearer token
```

Before the final commit point, failure restores the pre-enrollment configuration and restarts the previous service configuration.

Windows-side acceptance also checks configuration, token and listener prerequisites before network requests so diagnostic failures remain actionable rather than surfacing as generic PowerShell exceptions.
