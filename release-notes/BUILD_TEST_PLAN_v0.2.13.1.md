# Build and Test Plan — v0.2.13.1

## Compile
1. Open `PalworldServerManager.slnx` in Visual Studio.
2. Select `Release | x64`.
3. Clean the solution.
4. Rebuild the solution.

## Version validation
1. Start the application and confirm the window title ends in `v0.2.13.1`.
2. Confirm the left sidebar badge displays `v0.2.13.1`.
3. Locate `MystTiqPalworldServer.exe`, open Properties > Details, and confirm Product/File version `0.2.13.1`.
4. Run `./scripts/Get-ProjectVersion.ps1`; it must return `0.2.13.1`.
5. Run `./scripts/Package-Portable.ps1`; the output ZIP and staging folder must contain `v0.2.13.1`.
6. Confirm the package copies `release-notes/v0.2.13.1.md` as `README.txt`.

## Regression checks
- Dashboard opens normally.
- Notification bell toggles and hides after the final notification is cleared.
- Server start/stop controls still operate.
- World and player inspection still load.
- Base preview/export actions still work.

## Expected limitation
Portable data storage is not implemented in this version. The package is still a self-contained publish, but application-owned data paths retain existing behavior until v0.2.13.2.
