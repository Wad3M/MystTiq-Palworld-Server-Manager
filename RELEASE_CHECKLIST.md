### v0.4.18.2 Workspace + Diagnostics + Settings closeout acceptance
- [ ] Workspace refresh/validate/save uses the editable configuration API and rollback-safe headless persistence.
- [ ] Browse/open actions are local-profile-only; remote paths never invoke the GUI computer shell.
- [ ] Diagnostic copy/export/support ZIP behavior is present and secrets/URL credentials are redacted.
- [ ] LAN discovery, bearer token, TLS pin and explicit local bootstrap behavior remain intact.
- [ ] Every one of the 287 historical event inventory rows has an allowed classification, mapping and evidence.
- [ ] Shared card containment, centered metallic buttons, guarded relaunch, single polling, and remote security remain intact.

### v0.4.6.8 dashboard containment acceptance
- [ ] CPU/Memory compact meters remain clipped inside their dashboard status card at supported window sizes.
- [ ] Resource History drawing is clipped/inset and cannot paint beyond its owning border.

### Preserved tray-gate and reconstruction-roadmap acceptance
- [ ] Tray logic recognizes Show, Start/Restart/Stop, Safe Exit, Force Exit and Exit GUI Only.
- [ ] Reconstruction Guide, historical event inventory and functional crosswalk are packaged under `docs/reconstruction/`.
- [ ] `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md` begins feature restoration at v0.4.7.0 and enforces BACKEND REQUIRED for missing mutation services.
- [ ] Dangerous world/save mutations are documented as Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI.

# MystTiq Release Checklist

This is the active release checklist for MystTiq Palworld Server Manager. Historical version-specific acceptance criteria remain under [`docs/history/`](docs/history/).

### Current versioning policy

- Use `MAJOR.MINOR.REVISION.FIX`.
- Every new revision begins at fix `0` (for example `v0.4.4.0`).
- Gate failures remain on the same revision and increment only the final component (`v0.4.4.1`, `v0.4.4.3`, ...).
- Do not advance to the next revision until the complete promotion gate passes.
- Historical `FIX1`/`FIX2` labels remain unchanged as historical records; new releases do not use separate FIX suffixes.

## Current release state

- **Accepted source baseline:** v0.4.17.4
- **Current development candidate:** v0.4.18.2 — Workspace + Diagnostics + Settings Closeout
- **Next revision after promotion:** stop before v0.5.0.0
- **Shared GUI:** Avalonia desktop for Windows and Linux
- **Headless service:** Windows and Linux through shared Core/platform abstractions
- **Promotion status:** pending complete source and installed-tree gates

## Source and version

- [ ] `Directory.Build.props` contains `0.4.18.2`.
- [ ] Windows `app.manifest` is synchronized to `0.4.18.2`.
- [ ] `MystTiq.Core` targets plain `net10.0` and has no WPF dependency.
- [ ] `MystTiq.HeadlessHost` targets plain `net10.0` and references the shared core.
- [ ] `MystTiq.Desktop` remains the shared Avalonia Windows/Linux GUI.
- [ ] `README.md`, `CHANGELOG.md`, roadmap and release notes identify v0.4.18.2 as the current candidate and v0.4.17.4 as the baseline.
- [ ] `scripts\Test-v0.4.18.2-Logic.ps1` exists and `Build.ps1 LogicTests` resolves the current version dynamically.
- [ ] `SOURCE_MANIFEST_SHA256.txt` is regenerated after final changes.

## Required local build/test sequence

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.4.18.2-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Equivalent version-aware wrapper:

```powershell
.\scripts\Test-CurrentRelease.ps1 -ProjectRoot . -ExportJson
```

- [ ] Existing Windows WPF build succeeds.
- [ ] Shared core build succeeds.
- [ ] Headless host build succeeds.
- [ ] Existing Windows portable package / installer / checksums still succeed.
- [ ] v0.4.18.2 logic harness passes with zero failures.
- [ ] `/healthz` reports `component=mysttiq-headless`, `apiVersion>=1`, current backend version and platform.
- [ ] Local desktop-owned sidecar connects without bearer/TLS credentials and remote/LAN security remains unchanged.
- [ ] If the configured loopback port is occupied/incompatible, the GUI selects a private loopback sidecar endpoint.
- [ ] GUI reaches **Connected** and Start Server reaches the lifecycle endpoint.

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

## v0.3.0.7 FIX4 documentation gate

- [ ] `--help` lists `production-doctor`
- [ ] Linux command reference covers all headless commands
- [ ] README covers first-run, upgrade, Doctor and remote workflows
- [ ] Ubuntu 24.04.4 LTS remains documented
- [ ] roadmap contains v0.3.1.x Avalonia line
- [ ] v0.3.1.0 is Avalonia Desktop Foundation
- [ ] GUI architecture does not own PalServer lifetime
- [ ] clean-install acceptance remains final v0.3.0.7 gate

## v0.3.1.0 FIX1 desktop compile gate

- [ ] validation reports 0 errors / 0 warnings
- [ ] `Avalonia.Fonts.Inter` package is present when `.WithInterFont()` is used
- [ ] Windows Avalonia desktop publish succeeds
- [ ] Linux Avalonia desktop publish succeeds
- [ ] v0.3.1.0 logic harness reports 0 failures
- [ ] docs index displays v0.3.1.0 candidate


## v0.3.1.1 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows Avalonia publish succeeds
- [ ] Linux Avalonia publish succeeds
- [ ] all seven navigation pages are reachable
- [ ] local profile survives restart
- [ ] remote URL/profile metadata survives restart
- [ ] bearer token does NOT survive restart
- [ ] TLS SHA-256 pin accepts only exact matching certificate
- [ ] Linux deploy helper verifies SHA256 before extraction
- [ ] Linux XFCE smoke launch succeeds
- [ ] closing GUI leaves MystTiq service/PalServer running


## v0.3.1.3 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows Avalonia publish succeeds
- [ ] Linux Avalonia publish succeeds
- [ ] Linux headless publish succeeds
- [ ] player endpoint returns sanitized snapshot or clear unavailable state
- [ ] AdminPassword never appears in MystTiq player API output
- [ ] log-tail endpoint remains bounded
- [ ] metrics endpoint reports PalServer runtime evidence
- [ ] Players page works on Windows and Linux
- [ ] Monitoring page works on Windows and Linux
- [ ] v0.3.1.2 lifecycle regression passes


## v0.3.1.4 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows desktop publish succeeds
- [ ] Linux desktop publish succeeds
- [ ] Linux headless publish succeeds
- [ ] backup inventory works on live Linux host
- [ ] Backup Now produces verified managed archive
- [ ] delete cannot escape BackupRoot
- [ ] restore refuses while PalServer is running
- [ ] restore creates safety backup
- [ ] editable config excludes secret-file fields
- [ ] invalid configuration is rejected
- [ ] valid configuration writes rollback `.bak`
- [ ] auth/TLS remain enabled after configuration edit/restart
- [ ] lifecycle/players/monitoring regressions pass


## v0.3.1.6 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows desktop publish succeeds
- [ ] Linux desktop publish succeeds
- [ ] Linux headless publish succeeds
- [ ] distribution status reports configured SteamCMD/server evidence
- [ ] plan preview is non-mutating
- [ ] update refuses while PalServer runs
- [ ] stopped-server update/validate succeeds
- [ ] PalServer executable verified after SteamCMD
- [ ] lifecycle readiness returns after restart
- [ ] Doctor/Players/Monitoring/Backups/Configuration regressions pass


## v0.3.1.7 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows desktop publish succeeds
- [ ] Linux desktop publish succeeds
- [ ] Linux headless publish succeeds
- [ ] World Explorer endpoint returns HTTP 200
- [ ] expected active world resolves
- [ ] Level.sav and player saves are categorized correctly
- [ ] explorer exposes metadata only and no write/delete mutation
- [ ] secured extended lifecycle is SKIP rather than WARN when inapplicable
- [ ] Linux acceptance warnings 0
- [ ] Setup/Update, Doctor, Players, Monitoring, Backups and lifecycle regressions pass


## v0.3.1.8 promotion gate

- [ ] validation 0 errors / 0 warnings
- [ ] logic harness 0 failures
- [ ] Windows desktop publish succeeds
- [ ] Linux desktop publish succeeds
- [ ] Linux headless publish succeeds
- [ ] player-save identities match active world
- [ ] no invalid/non-hex player save filenames are accepted
- [ ] decoded GroupSaveDataMap guild semantics match known world when available
- [ ] missing decoded semantic JSON degrades explicitly without guessed guild data
- [ ] live REST evidence is enrichment only
- [ ] explorer remains read-only
- [ ] World Explorer/Doctor/Monitoring/Backups/lifecycle regressions pass

- [ ] `Test-v0.4.6.8-LinuxAcceptance.sh` and `Test-v0.4.6.8-ProductionReadiness.sh` are present before Linux packaging.
- [ ] Windows desktop publish launches the GUI only after success; unattended/release builds use `-NoGuiLaunch`.

- [ ] For v0.4.4.3+, `Build.ps1 Clean` leaves no `artifacts` directory; auto-launched development GUI/sidecar processes under `artifacts` are stopped without terminating PalServer or installed MystTiq services.

### v0.4.6.8 GUI branding/navigation acceptance
- [ ] Established MystTiq logo is displayed in the sidebar; temporary letter-mark logo is absent.
- [ ] Window/application icon uses the established MystTiq icon asset.
- [ ] Expanded child navigation rows remain 44 px high and receive only the standardized child indent.
- [ ] Branding/navigation changes render consistently on Windows and Linux Avalonia builds.

### v0.4.6.8 tray/lifecycle/page runtime acceptance

- [ ] Closing the main window hides it and leaves a MystTiq tray icon available.
- [ ] Tray **Show MystTiq** restores the same GUI instance.
- [ ] Tray Start/Restart/Stop route through the management API and reflect results in the GUI/status poll.
- [ ] **Exit GUI (keep backend running)** exits the desktop without stopping PalServer or the backend.
- [ ] **Stop Local Management Backend & Exit** stops only a sidecar started by the current GUI session; it never stops PalServer or an installed MystTiq service.
- [ ] Start Server remains retryable after an operation-level failure while the API is still reachable.
- [ ] Start Server auto-recovers the local sidecar when possible and displays a useful preflight/lifecycle error when PalServer cannot be launched.
- [ ] Server Setup / Update Center, Bases / Guilds, Console / Activity & Audit, and MOD Dashboard / MOD Library / UE4SS are visibly distinct pages rather than duplicate generic screens.


## v0.4.6.8 runtime interaction acceptance
- [ ] Hover/context interaction acceptance: buttons and selection controls provide explanatory tooltips.
- [ ] Player administration acceptance: right-click online player Kick/Ban reaches Palworld REST; unsupported provider actions fail visibly without fake success.
- [ ] Activity/Audit acceptance: management actions persist to MystTiq activity/audit logs and reload in the Activity & Audit page.
- [ ] Console capture acceptance: a MystTiq-launched Windows PalServer creates/updates `MystTiq-PalServer-Console.log` when stdout/stderr is available.
- [ ] World Inspector acceptance: only canonical SaveGames worlds are listed; backup snapshots are excluded; full World ID remains visible.

## Runtime parity continuation

- Configuration now includes a real active `PalWorldSettings.ini` OptionSettings editor through `/api/v1/palworld/config`.
- Save creates a timestamped rollback copy under `ConfigBackups`.
- PalServer readiness follows `PublicPort` from the active Palworld configuration instead of assuming UDP 8211.
- Generic/self-explanatory tooltips were removed; explanatory and safety tooltips remain.
- `docs/GUI_PARITY_v0.4.6.8.md` is the authoritative page-by-page legacy parity audit.
- Promotion still requires 0 validation warnings, v0.4.6.8 logic/runtime smoke, and manual Start Server + configuration-save acceptance.
- [ ] Avalonia configuration ListBox compiles using supported `ListBoxItem` stretching (no `ListBox.HorizontalContentAlignment`).

- v0.4.6.8 connection contract fix: desktop lifecycle DTO now accepts nullable `lastTransitionAt`, matching the headless/Core wire model so a stopped server can establish the management session before its first lifecycle transition.

## v0.4.6.8 dashboard/deployment acceptance

- [ ] `Update-FromDownloads.ps1` chooses the newest FullSource ZIP from Downloads, stages and validates it before deleting the installed source tree.
- [ ] Update helper runs the current `Build.ps1 Clean` before clearing the target and does not preserve stale source/build output.
- [ ] CPU card renders a history graph rather than a single percentage progress bar.
- [ ] Active World shows nickname plus complete canonical ID and Copy World ID places the complete ID on the clipboard.
- [ ] World Pulse shows authoritative Day/time when decoded GameDateTimeTicks evidence exists, plus last save and backup age.
- [ ] Live Console uses green text and includes both redirected stdout/stderr and Pal.log when both sources exist.
- [ ] Dashboard has one Dashboard heading and shows configured Server Name and Description.
- [ ] Persistent sidebar status has no label/value overlap at supported minimum window width.


## v0.4.6.8 Server Setup / navigation / updater acceptance

- [ ] Server Setup visually follows the v0.2.16.4 environment-checklist layout with Action, Component, Status, Location and Details.
- [ ] Environment Health ready/attention count comes from `/api/v1/server/environment`.
- [ ] Check for Updates, Install Missing and Verify Files update the Operation Monitor instead of acting as decorative buttons.
- [ ] Missing SteamCMD can be provisioned before Palworld server distribution update/install.
- [ ] Selected navigation item remains highlighted and the correct parent group expands after both direct clicks and programmatic navigation.
- [ ] Dashboard Live Activity is populated from the live console/log tail rather than duplicating a status line every poll.
- [ ] MystTiq console includes lifecycle launch path, arguments, PID/readiness and available Pal.log/AdminCommands evidence.
- [ ] `Update-FromDownloads.ps1` stages/validates the newest FullSource, cleans the old tree, replaces it, and automatically runs `Test-CurrentRelease.ps1` unless `-SkipBuild` is explicitly supplied.
- [ ] Page-by-page parity matrix is updated after each legacy window-set pass.

## v0.4.9.1 Configuration parity acceptance
- Simple/Advanced modes show the same authoritative settings.
- Search/category filtering is client-side only.
- Presets never change identity/password/port fields.
- Import changes editor state only; Save Changes performs the server-side write and rollback backup.
- Validation errors block Save Changes.
