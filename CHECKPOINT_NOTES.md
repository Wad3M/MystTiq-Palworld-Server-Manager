<!-- MystTiq v1.0.0.3: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.0.3 Checkpoint: PalDefender on the MODs Page, Drag and Drop Fixed

You reported that PalDefender doesn't show on the MODs page, and that dragging a ZIP onto the MODs page didn't install it.

## What was wrong on your server

- **PalDefender and UE4SS weren't loading at all.** Their loaders had been renamed by hand to `d3d9.dll.disabled-test`
  and `dwmapi.dll.disabled-test`, most likely during the 2026-10-01 stuck-server troubleshooting. PalDefender's last log
  was 2026-10-01 09:40. Since then Give Item, kits and teleports wouldn't have worked, and none of your UE4SS MODs loaded.
- With your go-ahead, I renamed both back with the server stopped. An older July `dwmapi.dll.myst-disabled` sits next to
  them; I left it alone.
- You have PalDefender 1.9.2. 1.9.3 came out on 2026-10-03.

## What's new

- **NATIVE MODs on the MODs page:** PalDefender and the UE4SS loader, with their version, whether they loaded since the
  server started, and an on/off switch. Switching off renames the loader to `*.mysttiq-disabled`. Switching on restores
  that copy first, otherwise the newest switched-off copy, which handles your hand-renamed files and never picks the
  July one.
- If the UE4SS loader is off, every enabled UE4SS MOD is marked "UE4SS loader is off" instead of looking fine.
- Disable All, Enable All and the stuck-start tests include them, so "Test without MODs" now also leaves PalDefender
  and UE4SS out. That's the same test that was done by hand on 2026-10-01.
- Delete, rollback and repair don't apply to them. Dropping PalDefender's ZIP explains where its files go, rather than
  installing it as a UE4SS folder that would never load.

## Drag and drop

The drop box had no background. The app only registers a drop where something is painted, so it worked only right on
the button or the caption under it. Drops anywhere else went to the card behind it. Now the whole box takes the drop.
The test harness drops a real ZIP on the box's empty corner. It failed before the fix and passes now.

## Verification

- **Full gate** `scripts\Test-v1.0.0.3-Logic.ps1 -RunBuild`: 269 / 269 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.2 check is carried, and the frozen v1.0.0.2 gate passes on its own checkpoint.
- **Static gate:** 220 / 220. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **NATIVE MOD smoke** `Test-v1.0.0.3-RouteSmoke.ps1`: 4 / 4. It covers your layout: both loaders renamed by hand, the July copy left alone, MystTiq's own copy restored first, disable/enable all, and delete and ZIP install refused.
- **Logic harness:** the NATIVE MOD scenario passes. **ArtworkHarness:** 759 checks pass, including a ZIP dropped on the empty corner of the drop box. That check failed before the fix.
- **Live, read-only on your data:** the published v1.0.0.3 MOD Dashboard and MOD Library list PalDefender (v1.9.2, `Win64\PalDefender.dll`) and the UE4SS loader as enabled NATIVE MODs.
- **Not verified live:** a real drag from Explorer (it would install into your server), and PalDefender loading on the next server start. Both need you.
- **Tray Exit, live (yours):** after Exit, no MystTiq process of yours was left. The `mysttiq-server.exe` you saw was the gate's own test server (parent: the gate's PowerShell; config under `artifacts\runtime-smoke`).

## For you

- **Start the main server once** and check the MODs page: PalDefender and UE4SS-Loader should be NATIVE and "Healthy"
  (loaded). If the server hangs on start, the Dashboard's stuck-start test will now include them.
- **Try dragging a MOD ZIP** onto the install box from Explorer. Don't run MystTiq with "Run as administrator": Windows
  blocks drag and drop from a normal Explorer window into an elevated one.
- **Queued for v1.0.1.0** (roadmap): an Update button for every Update Center component, greyed out where the
  component updates itself. PalDefender's would bring it to 1.9.3.
- **Pushing:** v1.0.0.3 hasn't been pushed. Say when.
