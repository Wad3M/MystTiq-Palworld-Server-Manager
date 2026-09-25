# v0.7.98.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`, on its output) so the route smokes exercise the new headless binary.
2. Run strict validation and `scripts/Test-v0.7.98.0-Logic.ps1 -RunBuild`, including the frozen v0.7.97.0
   checkpoint regression gate, the carried-forward smoke suites, the v0.7.97.0 and new v0.7.98.0 route smokes,
   and `MystTiq.LogicHarness` (now including 5 Doctor scenarios).
3. New surface: `DoctorHealthRules`, the four new finding groups in the unified diagnostics report.
4. Real-data check (read-only): read `/diagnostics/report` for the Default Server and confirm each new finding
   against the files (disk figures, memory, password set, backup age against the world's last write).
5. Not covered without GUI automation: how the Doctor page renders the new categories. Open Server Doctor, run
   it, and check the Resources, Backups, Security and Stability rows read sensibly; the Dashboard badge should
   read DEGRADED while a running server has a warning.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
