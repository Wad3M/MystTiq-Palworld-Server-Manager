# MystTiq UI Standards — v0.2.14.3

## Button density variants

- `MystTiqStandardButton`: normal page actions.
- `MystTiqCompactButton`: dense toolbars and multi-action cards.
- `MystTiqToolbarButton`: top command bars.
- `MystTiqWideActionButton`: primary full-row actions.
- `MystTiqIconButton`: icon-only controls such as notification actions.
- `MystTiqDataGridActionButton`: row-level DataGrid actions.

Semantic color styles such as `SuccessButton`, `WarningButton`, `DangerButton`, `InfoButton`, `RefreshButton`, and `DiagnosticButton` remain authoritative. Density variants control sizing only and should be combined through `BasedOn` styles rather than hard-coded page dimensions.

## Tooltips

Tooltips use the global MystTiq tooltip style, a 350 ms initial delay, and clear action-oriented wording.

## Dialogs

All application dialogs route through `AppDialog` / `IDialogService`. Direct `MessageBox.Show` calls outside the dialog service are prohibited.
