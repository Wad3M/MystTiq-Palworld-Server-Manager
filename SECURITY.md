# Security Policy

## Supported version

Security fixes are currently provided for the latest public release.

## Reporting a vulnerability

Do not post active credentials, private save files, server passwords, tokens, or exploitable details in a public issue. Use GitHub's private vulnerability reporting feature when enabled.

Include:

- affected version;
- reproduction steps;
- expected and actual behavior;
- potential impact;
- a redacted log or screenshot when useful.

## Credential exposure

If a credential was committed or uploaded, revoke or rotate it immediately. Removing it from a later commit does not remove it from Git history.


_Last reviewed for v0.2.14.11._


## Management API security

The v0.3 Linux management API defaults to loopback-only operation. Starting with v0.3.0.4, non-loopback binding is rejected unless both bearer-token authentication and TLS are enabled.

API tokens and certificate passwords must be stored in protected secret files, not embedded directly in `mysttiq.json`, source files, command histories, or release artifacts. Never commit files from `/etc/mysttiq/secrets/` or private certificate material.


## Remote API enrollment

Remote management remains disabled by default.

v0.3.0.6 requires explicit enrollment before binding MystTiq to a non-loopback address. The configuration and runtime both require authentication and TLS.

The test-environment certificate generator creates a self-signed certificate. Self-signed certificate chain validation may be bypassed only by the dedicated acceptance tooling for the disposable test environment. Production clients should use a trusted certificate chain rather than normalizing `SkipCertificateCheck`.

MystTiq does not automatically open Linux firewall ports during remote enrollment. Network exposure remains an administrator decision.

Private API bearer tokens, certificate passwords and PFX private-key material must not be committed to source control or included in diagnostic exports.
