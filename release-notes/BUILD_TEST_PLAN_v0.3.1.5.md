# Build / Test Plan — v0.3.1.5

1. Apply cleanup and validate with zero errors.
2. Run the v0.3.1.5 logic harness with `-RunBuild -ExportJson`.
3. Confirm Windows Avalonia, Linux Avalonia, and Linux headless publish successfully.
4. Deploy with `Deploy-Test-MystTiqLinux.ps1 -Extended`.
5. In the GUI, connect to the secured Linux API, open Doctor, and run Doctor.
6. Confirm overall state, evidence, recommendations, and timestamp populate.
7. Export the diagnostic report and verify it contains no secrets.
8. Treat the existing `Extended API lifecycle :: skipped: current API uses TLS/auth/non-default bind` message as an expected warning when the production-secure listener is active.
