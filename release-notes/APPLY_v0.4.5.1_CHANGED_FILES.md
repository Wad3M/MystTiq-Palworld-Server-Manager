# Apply v0.4.5.1 Changed Files

Apply this package over the v0.4.5.0 candidate or replace the tree with the Full Source package. Official baseline remains v0.4.4.3 until the complete v0.4.5.1 gate and runtime acceptance pass.

Then run `BUILD_TEST_PLAN_v0.4.5.1.md`.


## Runtime acceptance continuation
This candidate additionally restores hover help, player context actions, persistent activity/audit logging, Windows console capture, and canonical world filtering. The full v0.4.5.1 logic/runtime gate remains promotion-blocking until clean.

## Runtime parity continuation

- Configuration now includes a real active `PalWorldSettings.ini` OptionSettings editor through `/api/v1/palworld/config`.
- Save creates a timestamped rollback copy under `ConfigBackups`.
- PalServer readiness follows `PublicPort` from the active Palworld configuration instead of assuming UDP 8211.
- Generic/self-explanatory tooltips were removed; explanatory and safety tooltips remain.
- `docs/GUI_PARITY_v0.4.5.1.md` is the authoritative page-by-page legacy parity audit.
- Promotion still requires 0 validation warnings, v0.4.5.1 logic/runtime smoke, and manual Start Server + configuration-save acceptance.
