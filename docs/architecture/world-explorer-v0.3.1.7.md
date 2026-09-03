# v0.3.1.7 World Explorer Architecture

## Boundary

The Avalonia client never traverses the Linux or Windows save filesystem directly.

```text
MystTiq.Desktop
        ↓ authenticated MystTiq API
MystTiq persistent service
        ↓ IServerPathProfile.SaveRoot
Palworld SaveGames
```

The server discovers directories containing `Level.sav`, selects the newest such world as the active world, and returns metadata only.

## Read-only guarantee

This phase contains no world/save write, delete, restore, rename or arbitrary file-download endpoint. It is an inventory foundation for later semantic World Explorer parity.

## Inventory limits

One active-world response is capped at 5,000 files. Paths returned to the client are relative to the selected active world, while the server-authoritative SaveRoot and active world path are informational evidence.
