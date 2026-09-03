# Apply v0.4.4.3 Changed Files

Apply this corrective package over the current v0.4.4.3 candidate source tree, preserving relative paths. Do not delete the new Core Windows lifecycle/session files, desktop bootstrapper, runtime-smoke script, or version-matched Linux acceptance scripts.

Then run `BUILD_TEST_PLAN_v0.4.4.3.md`. Any failure remains a v0.4.4.x fix and blocks promotion.

This corrective package also fixes stale v0.4.4.2 assertions in the v0.4.4.3 test harness and hardens `Build.ps1 Clean` against auto-launched artifact processes holding the publish tree open.
