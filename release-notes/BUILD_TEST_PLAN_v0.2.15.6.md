# Build & Test Plan — v0.2.15.6

## Build
Run from the repository root in PowerShell 7:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Required runtime tests
1. **Healthy normal start** — with the current known-good enabled MOD set, click Start. Confirm reconciliation logs, a Ready gate, PalServer launch, and normal 45-second MOD load tracking.
2. **Restart path** — restart a running server. Confirm the same pre-start gate executes before the new PalServer process launches.
3. **Maintenance-backup restart** — run the coordinated maintenance backup workflow and confirm the restart also passes through the gate.
4. **enabled.txt reconciliation** — create/restore an `enabled.txt` override in one user UE4SS MOD. Start normally. Confirm MystTiq neutralizes it, preserves/adds its `mods.txt` state, rescans, and starts if no other issue remains.
5. **Missing enabled MOD** — make an enabled managed MOD's required runtime file unavailable. Start normally. Expected: startup is blocked, PalServer does not launch, and the dialog/log explains the missing/deployment issue.
6. **Wrong active root** — place an enabled UE4SS MOD only in the legacy/non-active root. Expected: normal startup is blocked with an Active Mods Root recommendation.
7. **Reconciliation filesystem failure** — make the relevant UE4SS state file/folder non-writable. Expected: reconciliation warning causes the normal startup health gate to block rather than silently continuing.
8. **Start Without MODs** — while a MOD gate failure exists, use Start Without MODs. Expected: PalServer starts with `-NoMods`; the MOD gate is intentionally bypassed.
9. **Verification report export** — MOD Dashboard → VERIFY & SCAN ALL MODS, then EXPORT REPORT. Confirm Explorer selects a TXT report and a matching JSON report exists under the activity `mod-verification-reports` folder.
10. **Report content** — confirm report version is v0.2.15.6 and contains MOD health, runtime/files status, evidence, errors, and repair recommendations where applicable.
11. **Regression** — enable/disable/apply MOD states, Repair States, legacy migration, Open Mods Folder, Verify Selected, Verify & Scan All MODs, and compatibility scan still work.
12. **Theme/UX** — confirm EXPORT REPORT matches existing MystTiq button sizing, dark theme, spacing, hover behavior, and tooltip style.

## Pass criteria
- All three build commands complete without errors.
- Healthy modded starts are not blocked.
- Unsafe/indeterminate modded starts are blocked before PalServer launches.
- Start Without MODs remains available.
- Exported verification reports are readable and correct.
- No regressions in MOD Library/Dashboard state management.
