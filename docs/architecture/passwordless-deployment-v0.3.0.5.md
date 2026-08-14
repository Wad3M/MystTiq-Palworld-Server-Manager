# Passwordless Linux Deployment & SSH Trust Architecture — v0.3.0.5

## Goal

Routine MystTiq Linux deployment should not repeatedly request the Linux account password.

v0.3.0.5 uses a dedicated SSH identity for the Windows-development-host → Linux-test-VM trust relationship.

## One-time trust bootstrap

```powershell
.\scripts\Initialize-MystTiqLinuxSSH.ps1
```

Default identity:

```text
%USERPROFILE%\.ssh\mysttiq_linux_ed25519
```

Default target:

```text
mystroth@192.168.1.248
```

The bootstrap:

1. creates an Ed25519 key with `ssh-keygen` if one does not already exist
2. reads the public `.pub` key only
3. makes one normal interactive SSH connection to the Linux VM
4. appends the public key to `~/.ssh/authorized_keys` if it is not already present
5. applies appropriate `.ssh` / `authorized_keys` permissions
6. verifies key-only authentication using `BatchMode=yes`, `PreferredAuthentications=publickey`, and `PasswordAuthentication=no`

The private key is never transmitted to Linux.

## Normal deployment

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

When the dedicated identity exists and passes its key-only preflight, all SSH/SCP operations use it.

If the identity is missing or invalid, deployment fails closed with instructions to run the bootstrap. Interactive password fallback requires explicit `-AllowPasswordFallback`.

## Acceptance

The existing archive hash verification and automated Linux acceptance workflow remain authoritative. v0.3.0.5 changes deployment authentication, not server/runtime behavior.

## Future direction

For non-test/production systems, later security work may add host-key pinning, key rotation/revocation, per-node deployment identities and managed trust enrollment. Those are separate from this test-environment convenience foundation.
