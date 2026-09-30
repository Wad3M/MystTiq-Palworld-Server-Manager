<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# v0.9.8.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.8.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.7.0 gate (every earlier contract);
   - the v0.9.8.0 contracts: the title bar's fixed "»"/"+" columns and narrow brand, the component alert and its rule,
     firewall rules against the network in use, each adapter's subnet up to a /22, the Linux installer, the accounts
     upgrade smoke, the review stamp at v0.9.8.0;
   - the logic harness's new scenarios and the ArtworkHarness's title-bar checks at 950, 1100 and 1440 px;
   - `Test-v0.9.8.0-UpgradeAccounts.ps1` (accounts from v0.8.25.0 to this version and back);
   - every carried smoke, both harnesses and the Linux VM checks.
3. Live: the title bar on a narrow window; the Alert Center's new rule; a Linux install with its desktop launcher.
4. Publish, launch, check `/healthz` reports 0.9.8.0.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.