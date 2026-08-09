# MystTiq Architecture Refactor — v0.2.15.14

## Goal
Prepare the validated Windows codebase for later Linux support without changing current runtime behavior.

## Changes
- Added `ApplicationServiceComposition` as the explicit composition root for the core server/MOD/diagnostics graph.
- Removed direct construction of those core services from `MainWindow`.
- Added `IServerSessionInspector` as the platform-facing session/process inspection contract.
- Kept `ServerSessionInspector` as the Windows implementation.
- Updated `ServerService` to depend on the interface while preserving a default Windows implementation.

## Current boundary
`MainWindow -> ApplicationServiceComposition -> ServerService -> IServerSessionInspector -> ServerSessionInspector (Windows)`

Native UE4SS evidence continues to consume `ServerService` session snapshots and therefore remains insulated from the platform implementation.

## Next architectural step
Introduce additional platform contracts for process launching, executable/path resolution, process termination/window handling, and possibly SteamCMD execution before implementing Linux-specific behavior.
