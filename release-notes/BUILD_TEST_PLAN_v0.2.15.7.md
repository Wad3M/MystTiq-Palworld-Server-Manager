# Build / Test Plan — v0.2.15.7

## Compile
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Logic harness
```powershell
.\scripts\Test-v0.2.15.7-Logic.ps1 -ProjectRoot . -ExportJson
```

## Runtime acceptance
1. Start PalServer normally with the existing enabled MOD set.
2. Wait through the 45-second runtime evidence window and confirm expected UE4SS/Lua MODs show **Loaded**.
3. Leave the server running for at least 5 minutes. Refresh MOD Library repeatedly at ~60, 90, 120, 180, and 300 seconds. Loaded rows must remain Loaded.
4. Run **Verify & Scan All MODs**. Dashboard runtime state must agree with MOD Library.
5. Export a MOD verification report. Exported runtime state must agree with Library/Dashboard.
6. Open MOD Runtime diagnostics and confirm a non-empty runtime session ID, increasing revision when new runtime evidence is observed, and a current last-observed timestamp.
7. Stop PalServer. Runtime session must become inactive and Loaded must not remain latched as current-session evidence.
8. Start PalServer again. A new session ID must be created; prior-session evidence must not immediately pre-populate Loaded before current-session evidence is observed.
9. Exercise Restart and maintenance-backup restart. Each replacement process must get a new runtime session boundary.
10. Confirm **Start Without MODs** still works and does not inherit loaded MOD state.


## FIX1 compile regression
The initial RC produced CS0103 in `ModService.cs` because the two-argument constructor referenced the stale identifier `ue4ssRuntimeResolver`; it also produced CS8618/CS0169 for an unused nested verifier field. FIX1 corrects both. The next build must show zero compile errors and no warnings from these locations.

## FIX2 targeted regression
1. Start PalServer with the existing enabled MOD set and do not manually run Verify All first.
2. At ~45–60 seconds, refresh MOD Library. Expected UE4SS/Lua rows must transition to **Loaded** from the shared current-session runtime state.
3. Confirm MOD Dashboard no longer remains **Runtime Unverified** once the same positive shared evidence exists.
4. Repeat Library refreshes through 5 minutes. Loaded state must remain latched for the current session.
5. Click **REFRESH INFO** on an installed MOD. It must refresh local/runtime details and must not open a browser.
6. Click **SEARCH ONLINE** separately and confirm only that explicit action opens the web search.
7. Stop and start PalServer. Confirm the prior session does not pre-populate Loaded; current-session evidence must be reacquired.
8. Run the updated logic harness and require zero failures before promotion.
