# Architecture Refactor Report — v0.2.15.11

## Scope
Behavior-preserving MOD subsystem modularization on top of v0.2.15.10 FIX1.

## Before / After
- MainWindow.xaml.cs before: **7,063 lines**
- MainWindow.xaml.cs after: **5,885 lines**
- MOD-specific partial extracted: **1,070 lines**
- New application coordinator: `Services/ModCoordinator.cs` (**64 lines**)
- New Dashboard state/projection service: `Services/ModDashboardStateService.cs` (**190 lines**)
- New explicit workflow contracts: `Models/ModWorkflowModels.cs`

## Responsibility changes
### MainWindow
Remains the WPF composition/UI shell but no longer directly coordinates Verify All, compatibility scanning, verification export, or Dashboard row projection.

### ModCoordinator
Owns inventory refresh, single/all verification orchestration, compatibility orchestration, repair recommendation assembly, and verification-report export.

### ModDashboardStateService
Owns Dashboard row creation/merging, verification projection, compatibility merge, runtime-positive healing, and Dashboard summary calculations. It has no WPF control dependencies.

### MainWindow.ModCenter.cs
Contains MOD-specific UI event handlers and presentation binding logic, reducing the central window file and keeping MOD UI behavior grouped.

## Preserved contracts
- Existing `ModService` remains the operational facade for installation/state changes.
- Existing runtime evidence and NativeModuleEvidenceService behavior are unchanged.
- Existing startup lifecycle gate is unchanged.
- Existing settings/storage formats are unchanged.
- MystTiq dark theme, buttons, semantic colors, tooltips, and XAML are unchanged.

## Next modularization target
`v0.2.15.12` should apply the same facade/provider pattern to server lifecycle/process/session concerns before platform abstraction for Linux.
