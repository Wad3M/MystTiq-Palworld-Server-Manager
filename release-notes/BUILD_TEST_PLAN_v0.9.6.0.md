<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# v0.9.6.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v0.9.6.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.9.5.0 gate (every earlier contract);
   - the v0.9.6.0 contracts: the firewall read through its COM API with program, service and app-container rules left
     out, the tagged allow script and its old-port cleanup, the elevated fallback, the status route, the two Doctor
     findings, the wizard's `-port=` for a second server, the search's TCP sweep, ranges, progress and Cancel, the
     review stamp at v0.9.6.0;
   - the logic harness's new scenarios (rule matching, the allow script, the rule state, a real read of this computer's
     firewall, ranges, virtual adapters and typed ranges, a timed search with a stand-in service, Cancel, launch
     arguments, a status read during a stop);
   - `Test-v0.9.6.0-FirewallRoute.ps1` (two servers, one whose `-port=` and PublicPort differ; GET routes only);
   - every carried smoke, both harnesses (all 12 languages) and the Linux VM checks.
3. Live: the firewall state on Diagnostics and Settings for a real server; a server search listing its ranges and
   progress. Adding a rule changes this computer's security settings, so the owner clicks **Allow through Firewall**
   and confirms Windows' prompt.
4. Publish, launch, check `/healthz` reports 0.9.6.0.
5. `Build.ps1 Package`: Windows and Linux ZIPs and `SHA256SUMS.txt`.
6. Checkpoint the FullSource ZIP and verify it against its SHA-256 manifest.