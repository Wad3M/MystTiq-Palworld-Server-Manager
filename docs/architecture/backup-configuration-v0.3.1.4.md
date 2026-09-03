# v0.3.1.4 Backup & Configuration Architecture

## Backup authority

The Avalonia desktop does not touch the Linux save tree or backup directory directly.

```text
MystTiq.Desktop
      ↓ authenticated MystTiq API
MystTiq persistent service
      ├─ SaveRoot
      └─ BackupRoot
```

Backup paths are server-authoritative. Client-supplied paths are not accepted; destructive calls use a validated managed filename only.

Restore requires PalServer to be stopped. When live save data exists, MystTiq creates a pre-restore safety backup before replacing it.

## Configuration authority

The desktop edits a restricted configuration DTO. Authentication token paths and TLS certificate/password paths are deliberately excluded from the editable DTO.

The service reconstructs a complete `HeadlessConfiguration` by combining editable fields with the existing security structures, validates it, creates a rollback copy, then atomically replaces the JSON configuration.

Changing configuration does not mutate the currently running host in place. A system-service restart is required.
