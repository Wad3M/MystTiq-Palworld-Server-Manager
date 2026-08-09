# Build & Runtime Test Plan — v0.2.15.1

## Required build sequence

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: zero build errors. Review validation warnings before runtime testing.

## Resolver tests

### Test 1 — Modern layout
Create/retain both:
- `<Win64>\ue4ss\Mods`
- `<Win64>\Mods`

Launch MystTiq and inspect the manager session log.

Expected:
- `Active Mods Root` = `<Win64>\ue4ss\Mods`
- Detection Method = `Modern UE4SS layout`
- Legacy root is reported but does not replace the modern root.

### Test 2 — Legacy layout
On a test installation where `<Win64>\ue4ss\Mods` does not exist but `<Win64>\Mods` does:

Expected:
- `Active Mods Root` = `<Win64>\Mods`
- Detection Method = `Legacy UE4SS layout` unless runtime-log evidence supplies another path.

### Test 3 — Runtime log verification
With a UE4SS.log containing:

`Loading mods from: C:\...\Win64\ue4ss\Mods`

Expected:
- `Runtime Mods Root` shows the parsed path.
- `Runtime Verified` = `True`.

### Test 4 — Mismatch health
If the manager resolves one path and UE4SS.log reports a different path:

Expected:
- Runtime Health = `Degraded`.
- Log includes `UE4SS Mod Root Mismatch` and both paths.

### Test 5 — Session consistency
Without calling a resolver refresh, repeated startup consumers should observe the same Active Mods Root for the current session.

## Regression test

Open MODs and UE4SS pages and confirm existing v0.2.14.11 behavior still operates. Phase 1 intentionally does not migrate MOD operations yet.
