# v0.2.13.1 FIX1 — Portable Packaging Parser Hotfix

## Corrected

PowerShell interpreted `$Version:` inside a double-quoted error message as an invalid scoped-variable reference. The package script now uses `${Version}:` so the colon is treated as normal text.

## Test

From the repository root:

```powershell
.\scripts\Package-Portable.ps1
```

Expected result:

- self-contained Windows x64 publish succeeds;
- portable staging directory is created under `artifacts`;
- `MystTiqPalworldServer-v0.2.13.1-win-x64-portable.zip` is created;
- `artifacts\SHA256SUMS.txt` is generated.
