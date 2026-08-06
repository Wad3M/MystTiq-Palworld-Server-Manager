# Build & Test Plan — v0.2.14.1 FIX4

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Player detection

1. Launch with the existing world configured.
2. Open Players and run Discover Saves.
3. Confirm only non-empty `*.sav` files whose basename is exactly 32 hexadecimal characters are imported.
4. Confirm `_dps`, temporary, recovery, malformed, and zero-byte files are ignored.
5. Confirm stale invalid records previously imported as `Imported save` disappear after restart.
6. Confirm legitimate online players and legitimate save-backed offline players still appear.

## Workspace UI

1. Open Server → Workspace at 100%, 125%, and 150% Windows scaling.
2. Confirm Refresh, Validate, Browse, Open, and Save buttons use compact standard sizes.
3. Confirm no text is clipped and button rows do not overflow.
4. Confirm all Workspace actions still work.

## Configuration density

1. Open Configuration → Simple Settings.
2. Confirm the header, search/filter row, identity card, preset card, and QoL rows are more compact.
3. Confirm substantially more World settings fit vertically on screen.
4. Verify sliders, checkboxes, text fields, Death Penalty, presets, Import, Export, Load, Reset, and Save all still work.
5. Confirm tooltips and dark-theme styling remain consistent.

## Regression

Verify Dashboard startup totals, notification diagnostics, coordinated live backup, Players, Guilds, Bases, World Inspector, MOD tools, Workspace persistence, and portable startup.
