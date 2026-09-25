# v0.7.97.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build so the
   route smokes exercise the new headless binary (they run against the published exe).
2. Run strict validation and `scripts/Test-v0.7.97.0-Logic.ps1 -RunBuild`, including the frozen v0.7.96.0
   checkpoint regression gate, the carried-forward smoke suites, the new
   `Test-v0.7.97.0-RouteSmoke.ps1` and `MystTiq.LogicHarness` (now including 7 crash scenarios).
3. New surface: `CrashSignatureCatalog`, `CrashAnalysisBuilder`, the analyze route's mod names, optional finding
   fields, the Crash Analyzer detail pane, per-server clearing on tab switch.
4. Real-data check: copy the Default Server's real log into an isolated fixture and analyze it (expect no
   findings on a healthy server). The real server is not touched.
5. Not covered without GUI automation: how the page renders. Open Crash Analyzer, run an analysis, pick a
   finding, and check the cause, what to try, mods, timing and evidence read sensibly; switch tabs and confirm
   no other server's findings remain.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
