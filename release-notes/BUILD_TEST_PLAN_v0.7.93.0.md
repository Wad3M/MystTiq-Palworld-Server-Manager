# v0.7.93.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and `scripts/Test-v0.7.93.0-Logic.ps1 -RunBuild`, including the frozen v0.7.92.0 checkpoint regression gate, the carried-forward smoke suites, and `MystTiq.LogicHarness` (now including the 5 `NexusModsLinks` scenarios).
3. New surface: `NexusModsLinks` (Core), `NexusModsClient` (Desktop), Nexus card and pipeline in `MainWindowViewModel`/`MainWindow.axaml`.
4. Live check: the Nexus v1 endpoint paths answer 401 to an invalid key with the required headers.
5. Not covered without an API key: authenticated JSON mapping and a real download-and-install; noted in the checkpoint.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
