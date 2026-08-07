# Build & Test Plan — v0.2.14.7

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Diagnostics Center

1. Open **Tools → Diagnostics Center**.
2. Confirm the page follows the MystTiq dark theme and shared button standards.
3. Run **Full Diagnostics** with PalServer stopped.
4. Run it again with PalServer running.
5. Verify findings are categorized and the score/totals update.
6. Confirm warnings explain the recommended action.
7. Export JSON/TXT and open the diagnostics folder.
8. Create a support package and inspect its contents.
9. Confirm `Configuration-REDACTED.json` does not contain the protected password value.
10. Confirm no world, player, or Level.sav timestamp changes.

## Regression

- Transaction Center loads and filters records.
- Repair Center remains preview-only.
- World Management wizard opens.
- Player identity deduplication remains effective.
- Startup Players/Guilds/Bases populate.
- Notification self-test and final bell auto-hide work.
- Coordinated live backup works.
- Installed and portable builds start normally.
