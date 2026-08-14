# MystTiq Release Checklist

This is the active release checklist for MystTiq Palworld Server Manager. Historical version-specific acceptance criteria remain under [`docs/history/`](docs/history/).

## Current release state

- **Official validated Windows baseline:** v0.2.16.4
- **Official Linux/headless baseline:** v0.3.0.5
- **Current development candidate:** v0.3.0.6 — Secure Remote API Enrollment & TLS Provisioning
- **Supported production GUI platform:** Windows 10/11 x64
- **Experimental Linux reference:** Ubuntu Server 24.04.4 LTS x86_64
- **Linux production support:** not yet declared; v0.3 is the implementation/parity line

## Source and version

- [ ] `Directory.Build.props` contains `0.3.0.2`.
- [ ] Windows `app.manifest` is synchronized to the development candidate.
- [ ] `MystTiq.Core` targets plain `net10.0` and has no WPF dependency.
- [ ] `MystTiq.HeadlessHost` targets plain `net10.0` and references only the shared core.
- [ ] `README.md` and `docs/index.html` distinguish the frozen Windows baseline from experimental Linux development.
- [ ] Only the current release logic harness remains active in `scripts/`.
- [ ] `SOURCE_MANIFEST_SHA256.txt` is regenerated after final changes.

## Required Windows build sequence

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

- [ ] Existing Windows WPF build succeeds.
- [ ] Shared core build succeeds.
- [ ] Headless host build succeeds.
- [ ] Existing Windows portable package / installer / checksums still succeed.
- [ ] v0.3.0.2 logic harness passes with zero failures.

## Linux headless publish

```powershell
.\Build.ps1 LinuxHeadless
```

- [ ] `linux-x64` self-contained headless publish succeeds.
- [ ] Linux `.tar.gz` archive is produced when `tar` is available.
- [ ] Headless binary runs on the Ubuntu reference VM without WPF/desktop dependencies.

## v0.3.0.6 remote API acceptance

- [ ] Windows build/regression gate passes with zero errors/warnings.
- [ ] Linux self-contained publish includes v0.3.0.6 acceptance and remote-enrollment scripts.
- [ ] passwordless deployment/extended Linux acceptance reports zero FAIL entries.
- [ ] temporary TLS certificate provisioning passes in the automated Linux runner.
- [ ] temporary explicit secured remote configuration validates.
- [ ] temporary remote configuration returns to loopback successfully.
- [ ] explicit Linux LAN enrollment completes with one sudo authorization.
- [ ] token/PFX/password files are owned by the service user and mode 0600.
- [ ] systemd unit verifies after remote enrollment.
- [ ] HTTPS health endpoint is reachable on the selected LAN address.
- [ ] unauthenticated Windows LAN management request receives HTTP 401.
- [ ] bearer-authenticated Windows LAN request receives HTTP 200.
- [ ] lifecycle JSON is returned through the secured LAN API.
- [ ] MystTiq does not silently modify firewall rules.
- [ ] one-command remote-disable returns API to `127.0.0.1:8213`.
- [ ] API schema/auth/TLS fail-closed behavior remains intact.
- [ ] Windows WPF behavior remains unchanged.

## Promotion gate

Promote v0.3.0.6 only after the normal automated Linux gate plus the explicit LAN enrollment, Windows LAN acceptance, and loopback rollback pass on the disposable Ubuntu VM.


## v0.3.0.6 FIX1 reliability gate

- [ ] enrollment script verifies remote commands before mutation
- [ ] pre-enrollment config backup is created
- [ ] token is generated and verified without manual intervention
- [ ] certificate and password secret are generated and verified without manual intervention
- [ ] secret/PFX owner and mode are checked explicitly
- [ ] written remote configuration validates
- [ ] effective config readback confirms requested LAN bind + auth + TLS
- [ ] exact LAN listener is verified before enrollment PASS
- [ ] local HTTPS health passes
- [ ] local unauthenticated management request returns 401
- [ ] local authenticated management request returns 200
- [ ] Windows acceptance produces clean FAIL results when prerequisites are intentionally absent
- [ ] Windows LAN acceptance passes after enrollment
- [ ] no manual token/certificate/config repair is required
- [ ] rollback returns the service to the prior configuration if enrollment fails before commit


## v0.3.0.6 FIX5 final harness gate

- [ ] release validation reports 0 errors / 0 warnings
- [ ] logic harness reports 0 failures
- [ ] enrollment-version check passes
- [ ] token-persistence semantic check passes
- [ ] prior Linux acceptance remains 26 passed / 0 failed / 0 warnings
- [ ] prior Windows LAN acceptance remains 10 passed / 0 failed
- [ ] no runtime code changed after those acceptance passes


## v0.3.0.7 promotion gate
- [ ] Windows validation: 0 errors / 0 warnings
- [ ] v0.3.0.7 logic harness: 0 failures
- [ ] Linux package contains first-run, upgrade, acceptance and production-readiness scripts
- [ ] Existing-VM upgrade acceptance passes
- [ ] Production Doctor/readiness report passes
- [ ] Reboot/systemd recovery remains healthy
- [ ] Clean/disposable Ubuntu 24.04.4 LTS first-run acceptance passes
- [ ] No configuration, secret/TLS, save or backup data is lost during upgrade


## v0.3.0.7 FIX2 integration gate

- [ ] Windows validation: 0 errors / 0 warnings
- [ ] v0.3.0.7 logic harness: 0 failures
- [ ] extended Linux acceptance contains the production-readiness invocation
- [ ] production readiness receives the current executable/config paths
- [ ] production-readiness output is captured in the acceptance report
- [ ] production-readiness failure blocks Linux acceptance
- [ ] extended deployment reports Production readiness integration PASS


## v0.3.0.7 FIX3 accounting gate

- [ ] Production Doctor remains 0 failures
- [ ] no PASS result is followed by a contradictory FAIL for the same check
- [ ] disk reserve emits exactly one threshold result
- [ ] production-readiness wrapper reports zero false failures
- [ ] extended Linux acceptance reports Production readiness integration PASS
