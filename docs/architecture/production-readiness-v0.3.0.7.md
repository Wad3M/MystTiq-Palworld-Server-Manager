# Linux Integration & Production Readiness — v0.3.0.7

v0.3.0.7 closes the planned v0.3 feature line with an integration gate.

## Production Doctor
`mysttiq-server production-doctor [--json]` reports each check with state, evidence and recommendation. Checks cover configuration, server root/entry, SteamCMD, backup root, systemd, PalServer readiness, disk reserve and management API security.

## First run
`Install-MystTiqLinux.sh` prepares MystTiq-owned directories, preserves existing configuration, migrates/validates schema and installs the systemd service.

## Upgrade
`Upgrade-MystTiqLinux.sh` creates a configuration rollback copy, migrates/validates configuration and uses the Linux-safe atomic service binary replacement established in v0.3.0.6 FIX4. It does not replace saves, backups, secrets or TLS material.

## Acceptance
Extended Linux acceptance invokes `Test-v0.3.0.7-ProductionReadiness.sh`, producing timestamped evidence instead of requiring a long manual command sequence.
