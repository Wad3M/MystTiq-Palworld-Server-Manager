# MystTiq Architecture Refactor — v0.2.15.15

## Platform boundary after this phase

MainWindow
→ ApplicationServiceComposition
→ ServerService
  → IServerSessionInspector
     → ServerSessionInspector (Windows)
  → IServerPlatformOperations
     → WindowsServerPlatformOperations

## Responsibilities

`IServerSessionInspector`
- session process tree
- loaded modules
- descendant processes
- guarded ports

`IServerPlatformOperations`
- executable resolution
- launch settings
- platform window behavior
- force-stop process-tree termination
- validated fallback process cleanup

`ServerLifecycleEvaluator`
- platform-neutral lifecycle state policy

## Linux roadmap

Remaining work before a Linux implementation should focus on:
- server executable/process-name profiles rather than hard-coded Windows names in discovery/resource monitoring
- SteamCMD invocation/platform packaging abstraction
- path conventions and deployment/install layout
- WPF/UI strategy for any true Linux manager build

The backend lifecycle boundaries are now substantially isolated from Windows-specific implementation details.
