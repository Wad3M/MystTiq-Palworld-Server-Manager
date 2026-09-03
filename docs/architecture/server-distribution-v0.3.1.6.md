# v0.3.1.6 Server Distribution Architecture

The desktop does not launch SteamCMD directly.

```text
MystTiq.Desktop
        ↓ authenticated MystTiq API
MystTiq persistent service
        ↓ IServerDistributionPlatformService
SteamCMD
        ↓
Palworld Dedicated Server files
```

The service owns configured SteamCMD and ServerRoot paths. The client may request status, a plan preview, or an explicit update/validation operation.

Update/validation is rejected while PalServer is running. The service serializes operations and validates that the PalServer executable exists after SteamCMD completes.

Linux retains `+@sSteamCmdForcePlatformType linux` through the shared Linux distribution implementation.

This workflow updates the Palworld Dedicated Server only. MystTiq application updates are a separate capability.
