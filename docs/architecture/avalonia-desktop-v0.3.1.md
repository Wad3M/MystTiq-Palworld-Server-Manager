# Avalonia Cross-Platform Desktop Architecture

Decision: **Avalonia UI + .NET 10 + C# + XAML** for MystTiq v0.3.1.x.

`MystTiq.Desktop` is an API client, not PalServer lifecycle authority.

```text
MystTiq.Desktop
   Views / ViewModels / API client
               ↓
         Management API
               ↓
     Persistent MystTiq service
               ↓
            PalServer
```

Closing or crashing the GUI must leave the service and PalServer unaffected.

## Shared

- views/layouts
- ViewModels
- API contracts/client
- themes/resources
- navigation
- Doctor/status presentation
- validation/workflows

## Thin platform adapters

- file/folder dialogs
- tray integration
- notifications
- startup/shortcuts
- OS packaging
- filesystem conventions
- local service discovery where needed

## Migration strategy

Keep the proven Windows WPF client throughout v0.3.1 development. Build Avalonia alongside it and use feature-parity/regression gates. Consider replacement only after the cross-platform client reaches sufficient Windows parity and stability.

## Deployment modes

1. Linux headless only
2. Linux GUI + local service
3. Windows GUI + local service
4. Windows/Linux GUI + remote secured service

This architecture also prepares the client for planned v0.6 multi-server support.
