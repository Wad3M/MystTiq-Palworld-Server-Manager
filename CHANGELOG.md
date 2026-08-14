# Changelog

## v0.3.0.7 FIX3 — Production Readiness Result Accounting Hotfix

- Fixed Bash post-increment exit-status behavior causing PASS checks to also execute FAIL branches.
- `record` now uses assignment-based counter increments and explicitly returns success.
- Executable and disk-reserve checks now use explicit `if / elif / else` control flow.
- Added regression coverage for production-readiness result accounting.
- The underlying Production Doctor remains unchanged and had already passed 9/9 checks.


## v0.3.0.7 FIX2 — Extended Production-Readiness Integration Hotfix

- Fixed extended Linux acceptance not invoking the packaged v0.3.0.7 production-readiness runner.
- Extended acceptance now passes the active executable and config paths into the production-readiness test.
- Production-readiness output is captured in the timestamped Linux acceptance report.
- A readiness failure now increments the Linux acceptance failure count and blocks promotion.
- No runtime/API/lifecycle/systemd/TLS/PalServer/Windows WPF behavior changed.


## v0.3.0.7 FIX1 — Production Doctor Compile & Release-Gate Hotfix

- Fixed Production Doctor references to the existing `IServerPathProfile` contract.
- Routed PalServer readiness through the established `LinuxServerLifecycleService`.
- Fixed the v0.3.0.7 logic harness architecture path.
- Removed active stale v0.3.0.6 HeadlessHost wording.
- Prevented the cleanup helper from creating stale-version warnings.
- Corrected apply order: Unblock PowerShell scripts before running cleanup.


## v0.3.0.7 — Linux Integration & Production Readiness

- Added production-doctor with evidence/recommendation output.
- Added first-run and safe-upgrade Linux automation.
- Added one-command production-readiness reporting.
- Integrated production readiness into extended Linux acceptance.
- Reserved v0.3.0.8+ for stabilization only.


## v0.3.0.6 FIX5 — Acceptance Harness Consistency Hotfix

- Fixed two false-negative logic-harness checks after successful real-world remote API acceptance.
- Enrollment-version validation now matches the stable v0.3.0.6 base contract instead of exact decorated FIX text.
- Token-persistence validation now checks stable semantic markers rather than exact spacing-sensitive output.
- Synchronized Linux enrollment and Windows remote-acceptance display labels to FIX5.
- No runtime/API/TLS/auth/systemd/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX4 — Running-Binary Service Install Hotfix

- Fixed Linux `service-install` failing with `Text file busy` while `/opt/mysttiq/bin/mysttiq-server` was running.
- Replaced in-place executable overwrite with same-directory staged copy plus atomic rename.
- Existing systemd restart behavior now launches the replaced binary while the old process can finish on its previous inode.
- Added regression coverage for staged executable replacement.
- No API, TLS, authentication, configuration schema, PalServer lifecycle policy, or Windows WPF behavior changed.


## v0.3.0.6 FIX3 — Protected Secrets Directory Ownership Hotfix

- Fixed `/etc/mysttiq/secrets` being created as root-only while MystTiq secrets were owned by the non-root service user.
- Secrets directory is now owned by the selected service account with mode 0700.
- Enrollment explicitly verifies secrets-directory owner/mode.
- Token/certificate existence checks now use privilege-safe `sudo test -s` checks.
- Preserved per-file mode 0600 and pre-commit rollback behavior.
- No runtime/API/security-policy/lifecycle/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX2 — Linux Remote-Tool Packaging Hotfix

- Fixed Linux headless archives omitting the v0.3.0.6 remote-enrollment and rollback scripts.
- `Build-LinuxHeadless.ps1` now packages the acceptance runner, remote enrollment script, and remote disable script.
- Missing required Linux-side scripts now fail the package build instead of producing an incomplete archive.
- Added regression coverage for all required Linux operational scripts.
- No runtime, API, security, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 FIX1 — Remote Enrollment Reliability Hotfix

- Fixed remote enrollment leaving MystTiq on the previous loopback/schema-v1 configuration.
- Reworked Linux enrollment into a staged fail-fast workflow with prerequisite, file, ownership, configuration, service, listener, HTTPS and authentication verification.
- Added automatic pre-enrollment configuration backup and pre-commit rollback.
- Enrollment now verifies token/PFX/password creation instead of assuming commands succeeded.
- Enrollment now reads the effective config back and requires the requested bind plus auth/TLS before restarting.
- Enrollment now verifies the exact LAN listener before reporting success.
- Windows remote acceptance now validates effective config, token and listener prerequisites before attempting HTTPS.
- Missing token/listener/connectivity now produces explicit `[FAIL]` output instead of null-reference PowerShell exceptions.
- No core API security policy, configuration schema, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 — Secure Remote API Enrollment & TLS Provisioning

- Promoted v0.3.0.5 as the official Linux/headless baseline.
- Added `HeadlessCertificateService` for self-signed TLS server certificate provisioning.
- Generated certificates use RSA 3072, SHA-256, server-auth EKU, selected IP SAN, localhost SAN, optional DNS SAN, and a maximum 825-day validity.
- PFX passwords are generated separately using the existing protected-secret service.
- Added `HeadlessRemoteApiEnrollmentService` for typed remote enable/disable configuration updates.
- Added `api-tls-create`, `api-remote-enable`, and `api-remote-disable`.
- Remote enable requires an explicit non-loopback literal IP and produces an auth+TLS configuration; remote disable returns to loopback defaults.
- Added `scripts/Configure-MystTiqRemoteApi.sh` for one-command Linux enrollment with confirmation, one sudo authorization, secret/certificate provisioning, service-user ownership hardening, systemd restart and HTTPS/auth validation.
- Added `scripts/Disable-MystTiqRemoteApi.sh` for one-command return to local-only management.
- Added `scripts/Test-MystTiqRemoteApi.ps1` for Windows-over-LAN HTTPS/401/bearer-auth acceptance using the dedicated SSH trust channel.
- Linux enrollment intentionally does not alter firewall rules.
- Extended the Linux acceptance runner with temporary TLS certificate creation and explicit secured remote-config/rollback tests.
- Windows WPF behavior remains unchanged.


## v0.3.0.5 — Passwordless Linux Deployment & SSH Trust Foundation

- Promoted v0.3.0.4 FIX1 as the official Linux/headless baseline.
- Added `scripts/Initialize-MystTiqLinuxSSH.ps1` for one-time dedicated Ed25519 deployment-key setup.
- The SSH bootstrap installs only the public key into `~/.ssh/authorized_keys`; the private key remains on Windows.
- Added passwordless-key verification using OpenSSH batch/public-key-only authentication.
- Reworked `scripts/Deploy-Test-MystTiqLinux.ps1` to prefer the dedicated MystTiq SSH key automatically.
- Deployment now uses the same dedicated identity for SSH and SCP.
- Interactive password deployment is disabled by default and available only through explicit `-AllowPasswordFallback`.
- Removed normal dependence on Posh-SSH/password caching from the deployment workflow.
- Preserved SHA-256 transfer validation, versioned extraction, and automated Linux acceptance invocation.
- Carried the version-matched Linux acceptance runner forward as `scripts/Test-v0.3.0.5-LinuxAcceptance.sh`.
- No management API, configuration schema, TLS/authentication, PalServer lifecycle, systemd, or Windows WPF behavior changed.


## v0.3.0.4 FIX1 — HeadlessHost Compile & Automation Harness Hotfix

- Fixed CS1929 by invoking `WaitForShutdownAsync(CancellationToken)` through the `IHost` generic-host interface.
- Corrected the deployment harness literal for the actual `& .\Build.ps1 LinuxHeadless` invocation.
- Prevented the release validator from treating the versioned Linux acceptance-script filename as a stale-version suffix.
- Added regression coverage for the `IHost` shutdown bridge.
- No security model, configuration schema, API behavior, lifecycle policy, Linux acceptance behavior, or Windows WPF behavior changed.


## v0.3.0.4 — Secure Management API & Automated Linux Acceptance Foundation

- Promoted v0.3.0.3 FIX1 as the official Linux/headless baseline.
- Advanced headless configuration to schema version 2 with API authentication and TLS settings.
- Added supported in-memory migration from schema v1 plus `config-migrate` persistence and automatic migration during `service-install`.
- Added protected secret-file service with cryptographically random bearer-token generation and Linux owner-only token permissions.
- Added `api-token-create` command.
- Added bearer-token authentication for `/api/v1/*` while keeping `/healthz` intentionally minimal and unauthenticated.
- Added fixed-time token comparison.
- Added TLS certificate/password-file support for Kestrel.
- Non-loopback API configuration now fails closed unless authentication and TLS are both enabled; the runtime API host independently enforces the same condition.
- Default configuration remains loopback-only and does not require authentication/TLS.
- Added version-matched `scripts/Test-v0.3.0.4-LinuxAcceptance.sh` with consolidated PASS/FAIL/WARN output plus timestamped raw evidence.
- Added `scripts/Deploy-Test-MystTiqLinux.ps1` to build, verify, copy, extract and invoke Linux acceptance in one workflow; default Linux test host is `192.168.1.248`.
- Linux publish archives now include their version-matched Linux acceptance runner.
- Added the detailed product roadmap through v0.8, including v0.4 character migration/Doctor/UI consolidation, v0.5 advanced administration, v0.6 multi-server, v0.7 themes/icon set and v0.8 adaptive efficiency.
- Windows WPF behavior remains unchanged.


## v0.3.0.3 FIX1 — Configuration Compile & systemd Argument-Quoting Hotfix

- Corrected Linux absolute-path validation to use `StartsWith("/", StringComparison.Ordinal)` instead of the invalid char/StringComparison overload.
- Added the missing `QuoteSystemdArgument(string value)` helper used by systemd `ExecStart` configuration-path generation.
- Added newline-injection rejection to the systemd argument-quoting helper.
- Strengthened the v0.3.0.3 harness to cover both compile contracts.
- No configuration schema, API, lifecycle, recovery, or Windows WPF behavior changed.


## v0.3.0.3 — Headless Configuration & Local Management API Foundation

- Promoted v0.3.0.2 FIX2 as the official Linux/headless baseline.
- Added schema-versioned persistent headless configuration with Linux defaults under `/etc/mysttiq/mysttiq.json`.
- Added configuration validation for paths, lifecycle/recovery values, API port, and API bind scope.
- v0.3.0.3 rejects non-loopback API binding by design.
- Added `config-show`, `config-validate`, and `config-write-default`.
- Added `config/mysttiq.linux.example.json`.
- Lifecycle CLI and service supervisor now consume configured timeouts, paths, recovery settings, and PalServer launch arguments.
- Added ASP.NET Core/Kestrel local management API support to the headless host.
- Added loopback-only health, lifecycle status, systemd status, configuration, and start/stop/restart endpoints.
- Lifecycle-changing API calls are serialized to prevent competing operations.
- `service-run` starts/stops the local API with the supervisor when API support is enabled.
- Added standalone `api-run` for local API testing without systemd.
- `service-install` creates a default configuration when one does not exist and preserves a custom `--config` path in systemd `ExecStart`.
- No remote/LAN API exposure, authentication secrets, or Windows WPF changes are introduced in this phase.
- Added Windows v0.4 backport candidates for shared configuration, local service IPC/API, and UI-to-background-service control.


## v0.3.0.2 FIX2 — systemd Start-Limit Section Hotfix

- Moved `StartLimitIntervalSec=300` and `StartLimitBurst=5` from `[Service]` to `[Unit]`.
- Preserved `Restart=on-failure` and `RestartSec=10` under `[Service]`.
- Added regression coverage for correct directive ordering.
- No lifecycle, recovery algorithm, PalServer launch, or Windows WPF behavior changed.


## v0.3.0.2 FIX1 — systemd Unit Raw-String Compile Hotfix

- Replaced the malformed raw-string `BuildUnit()` implementation with compile-safe string construction.
- Preserved the exact v0.3.0.2 systemd unit policy and lifecycle behavior.
- Strengthened the v0.3.0.2 harness to detect raw-string regression and verify generated unit sections.
- No Windows WPF behavior changed.


## v0.3.0.2 — Linux Service & Automatic Recovery Foundation

- Added systemd service models and `LinuxSystemdServiceManager`.
- Added `service-status`, `service-install`, `service-uninstall`, and internal `service-run` headless commands.
- Service installation copies the current self-contained headless host to `/opt/mysttiq/bin/mysttiq-server`, writes `/etc/systemd/system/mysttiq-palworld.service`, reloads systemd, and enables boot startup.
- Service start remains explicit unless `--start-now` is supplied.
- Added a long-running `LinuxHeadlessSupervisor` that starts/adopts PalServer, monitors lifecycle state, and performs bounded automatic crash recovery with configurable backoff/restart windows.
- Added SIGTERM/SIGINT handling so systemd shutdown asks MystTiq to perform the existing graceful PalServer shutdown policy.
- The service runs as the selected non-root service account, with `NoNewPrivileges=true`, `Restart=on-failure`, restart delay, and systemd start-rate limits.
- systemd journal output now captures MystTiq service/supervisor logs while PalServer console output remains under `/opt/mysttiq/runtime`.
- Promoted v0.3.0.1 FIX1 as the official Linux/headless baseline.
- Added v0.4 backport candidates for Windows Service supervision, automatic recovery throttling, background journal/event logging, and UI-independent startup recovery.


## v0.3.0.1 FIX1 — Linux Lifecycle Compile Contract Hotfix

- Added `FindProcessesByName(...)` to the shared `IServerSessionInspector` contract; the Linux implementation already provided the method and the lifecycle layer consumes it.
- Marked `LinuxServerLifecycleService` with `[SupportedOSPlatform("linux")]` so .NET platform analysis recognizes intentional Unix-only API use such as `File.SetUnixFileMode`.
- Strengthened the v0.3.0.1 harness to validate the shared process-discovery contract and Linux platform annotation.
- No lifecycle semantics or Windows behavior changed.


## v0.3.0.1 — Linux Server Lifecycle Control

- Added shared lifecycle phase, operation-result, persisted-state, and stable headless exit-code models.
- Added `LinuxServerLifecycleService` with `start`, `stop`, `restart`, and lifecycle-aware `status`.
- Linux start blocks duplicate PalServer instances, launches the server detached from the SSH terminal, observes the native `PalServer-Linux-Shipping` process, and verifies UDP 8211 before reporting ready.
- Detached PalServer stdout/stderr is written to `/opt/mysttiq/runtime/palserver-console.log`.
- Added persisted lifecycle state under `/opt/mysttiq/runtime/lifecycle-state.json` for transition/crash evidence across short-lived CLI invocations.
- Linux stop sends SIGTERM first and escalates to SIGKILL only after the graceful timeout; force escalation is surfaced in the operation result.
- Restart safely stops an active server before starting it; restart from stopped/crashed state proceeds as a start.
- Added Ctrl+C cancellation handling and configurable startup/stop timeouts to the headless CLI.
- Corrected Linux kernel reporting to read `/proc/sys/kernel/osrelease`; the validated host previously exposed distro text through `RuntimeInformation.OSDescription`.
- Preserved `probe` and informational `install-plan`, including the validated SteamCMD `+@sSteamCmdForcePlatformType linux` requirement.
- Windows WPF behavior remains unchanged; lifecycle/service backport opportunities are recorded for v0.4.

## v0.3.0.0 FIX1 — Regression Harness Reserved Variable Hotfix

- Corrected the v0.3.0.0 PowerShell harness to avoid the read-only built-in `$Host` variable by using `$headlessHostSource`.
- Runtime validation then passed 55/55 and the self-contained Linux host successfully detected Ubuntu, SteamCMD, `PalServer-Linux-Shipping`, and UDP 8211.


## v0.3.0.0 — Headless Core Foundation & Linux Platform Base

- Added `MystTiq.Core` as a new `net10.0` cross-platform project with no WPF dependency.
- Added `MystTiq.HeadlessHost` as a no-GUI command-line host for Linux/headless development.
- Added platform-neutral runtime configuration separate from the Windows WPF `AppSettings` model.
- Added Linux distribution detection through `/etc/os-release` plus kernel/architecture reporting.
- Added Linux server path profile using `/opt/mysttiq/palserver`, `/opt/mysttiq/steamcmd/steamcmd.sh`, LinuxServer config paths, logs, saves, and backup roots.
- Added Linux process/executable profile for `PalServer.sh` and `Pal/Binaries/Linux/PalServer-Linux-Shipping`.
- Added `LinuxServerDistributionPlatformService` with Valve Linux SteamCMD package handling and Palworld App `2394010`.
- Baked the validated `+@sSteamCmdForcePlatformType linux` SteamCMD override into Linux install/update argument generation after the reference environment returned `Missing configuration` without it.
- Added read-only Linux procfs session inspection for process trees, mapped files, and guarded TCP/UDP port observation.
- Added non-destructive headless commands: `probe`, `status`, and `install-plan`; v0.3.0.0 does not yet grant the headless host start/stop/install/update authority.
- Added a dedicated Linux self-contained publish workflow through `Build.ps1 LinuxHeadless`.
- Added Linux tested-environment documentation for Ubuntu Server 24.04.4 LTS x86_64, kernel `6.8.0-137-generic`, Valve Linux SteamCMD, and native PalServer validation.
- Added the formal SHARED / LINUX / WINDOWS-BACKPORT registry; Windows headless/service/minimized-resource improvements are reserved for v0.4 unless shared-core correctness requires earlier work.
- Preserved v0.2.16.4 as the frozen validated Windows baseline; no intentional WPF behavior changes are included in this phase.


## v0.2.16.4 — SteamCMD Distribution Abstraction & Final Windows Platform Audit

- Added `IServerDistributionPlatformService` as the platform boundary for SteamCMD package source, extraction, command arguments, process startup policy, and default Palworld install recovery.
- Added `WindowsServerDistributionPlatformService`, preserving the validated Windows SteamCMD behavior and Palworld Dedicated Server App ID 2394010.
- Added `ServerDistributionPlatformService.ForCurrentPlatform()` so higher-level installer/update services no longer construct the Windows implementation directly.
- Migrated `InstallerService` SteamCMD install/self-update and Palworld server install/retry/recovery logic to the shared distribution service.
- Migrated `SteamServerUpdateService` command construction and SteamCMD process startup to the shared distribution service.
- Application composition now creates one shared distribution service for server update and installer workflows.
- Centralized remaining core-service SteamCMD executable-name usage through `ServerPlatformProfile`.
- `ApplicationPathService` workspace/default SteamCMD discovery now uses the platform executable name instead of embedding `steamcmd.exe`.
- Server diagnostics now consume platform process names instead of hard-coded PalServer process names.
- Completed a final Windows platform audit and documented remaining WPF/UI-specific assumptions for the v0.3 Linux foundation.
- No intended changes to current Windows install/update semantics, server lifecycle, MOD evidence, Operational Health, World Inspector safety, or WORLD PULSE.
- Completed a documentation closeout after successful v0.2.16.4 build/runtime validation: removed historical implementation notes from the repository root, archived them under `docs/history/`, moved the current platform audit under `docs/architecture/`, moved publication-process material under `docs/release/`, and rebuilt README/RELEASE_CHECKLIST as current-state documents.
- Updated the public `docs/index.html` baseline/RC wording and Linux-support statement.


## v0.2.16.3 FIX2 — Legacy MystTiq Release-Source State Removal

- Removed the obsolete MystTiq release-source fallback from `FinalizeUpdateCheckResults()`.
- Removed the obsolete status from the generic Update Center action mapper.
- GitHub comparison failures now use the retryable `UNABLE TO CHECK` state.
- Strengthened regression coverage so the legacy state cannot silently return.
- No unrelated runtime behavior changed.


## v0.2.16.3 FIX1 — Update Center Regression Harness False-Positive Hotfix

- Corrected the v0.2.16.3 logic harness so the obsolete manager release-source placeholder check is scoped specifically to the `MystTiq Server Manager` component branch.
- Generic Update Center fallback compatibility text no longer causes a false failure.
- No application source, UI, update behavior, or runtime logic changed.


## v0.2.16.3 — Application Update Awareness & Server Setup Polish

- Added `MystTiqReleaseService` backed by the public GitHub `releases/latest` endpoint for `Wad3M/MystTiq-Palworld-Server-Manager`.
- Added numeric MystTiq version parsing/comparison with explicit `UPDATE AVAILABLE`, `UP TO DATE`, and `DEVELOPMENT BUILD` states.
- Server Setup `CHECK FOR UPDATES` now includes the installed MystTiq version and latest public GitHub release alongside SteamCMD, Palworld, UE4SS, and Workshop information.
- Update Center now performs the same MystTiq GitHub release check and opens the official Releases page when a newer manager release is available.
- GitHub/network failures are fail-soft and do not prevent the remaining component update checks.
- Reduced the Server Environment status badge column, padding, and font size; READY/MISSING/DISABLED/OPTIONAL badges now share compact centered geometry.
- Preserved existing button/theme/tooltip semantics, server lifecycle logic, live-save safety, MOD runtime evidence, Operational Health, and WORLD PULSE behavior.


## v0.2.16.2 — Live Save Read Safety & UI Button Standardization

- Added `SafeWorldSaveSnapshotService` to create stable temporary snapshots of live Palworld save files using `FileShare.ReadWrite | FileShare.Delete`.
- Snapshot creation verifies source length and last-write stability and retries around PalServer save/write windows.
- World Inspector header inspection now reads the stable snapshot instead of opening active `Level.sav` directly.
- Recoverable live-save contention is reported in the Inspector status area without a blocking modal error.
- Hardened `Plm1SaveDecoder` header reads with shared file access for other read-only callers.
- Reduced the shared MystTiq button footprint by approximately 10% while preserving semantic colors, common template behavior, tooltip standards, borders, rounded corners, hover/pressed behavior, and typography hierarchy.
- Normalized Workspace, MOD, Player, Config, icon, and DataGrid button variants to the shared density standard.
- Converted 42 button-only horizontal action clusters to equal-cell `UniformGrid` layouts for consistent grid alignment.
- Removed common per-button size overrides where the semantic/shared style should be authoritative.
- No intended changes to server lifecycle, MOD runtime evidence, Operational Health, World Pulse semantics, save contents, or destructive world-repair behavior.


## v0.2.16.1 — Runtime Path & Deployment Abstraction

- Introduced `IServerPathProfile` and `WindowsServerPathProfile`.
- Centralized deployment/runtime path construction and shared it through the composition root.
- Migrated core UE4SS/MOD/environment/installer/doctor services from direct Win64 path construction.
- Preserved validated Windows behavior; Linux implementation remains deferred.


## v0.2.15.17 — Live World Telemetry & Dashboard Pulse

- Added `WorldTelemetryService` with server-session-scoped player metrics: current online, peak online, joins, leaves, unique players, and last player transition.
- Added `WorldClockProvider` to read the authoritative saved Palworld clock from decoded `Level.sav` `GameTimeSaveData.GameDateTimeTicks`.
- Added `WorldTelemetrySnapshot` / `WorldClockSnapshot` models and centralized telemetry composition.
- Added `ServerService.ActiveSessionId` and `ActiveSessionStartedAt` read-only session metadata for true PalServer uptime.
- Added a Dashboard `WORLD PULSE` strip showing saved world day/time, session uptime, session player metrics, save freshness, latest backup age, and last player event.
- Player join/leave and saved-world day changes can flow into the existing Activity/Audit feed.
- The existing Dashboard UPTIME indicator now reflects the active PalServer session rather than MystTiq process uptime.
- World-clock values are never estimated from uptime; if authoritative save evidence is unavailable, MystTiq displays the clock as unavailable.
- No intended changes to MOD health/runtime evidence, server lifecycle semantics, dark theme, button standards, or tooltip standards.


## v0.2.15.16 — Platform Profile Abstraction & Documentation Consistency

- Added `ServerPlatformProfile` as the centralized source for PalServer process names, executable-relative paths, SteamCMD executable naming, and guarded ports.
- Removed hard-coded Windows PalServer process names and guarded-port constants from `ServerService`.
- `ServerProcessDiscoveryService`, `ServerResourceMonitor`, `ServerSessionInspector`, and `WindowsServerPlatformOperations` now receive their conventions through the selected platform profile.
- Preserved the Windows profile and all current Windows lifecycle behavior as the default.
- Performed a full README consistency rewrite: removed duplicate Feature Matrix/release-diary content, removed stale baseline/RC claims, consolidated MOD runtime-health semantics, corrected Linux wording, and made CHANGELOG/release-notes authoritative for historical version detail.
- No intended UI, dark-theme, button, tooltip, MOD evidence, operational-health, or lifecycle behavior changes.


## v0.2.15.15 — Server Platform Services Abstraction

- Added `IServerPlatformOperations` as the platform boundary for executable resolution, process launch policy, post-launch window handling, forced process-tree termination, and server-process fallback cleanup.
- Added `WindowsServerPlatformOperations` containing the existing Windows-specific implementation.
- `ServerService` now depends on `IServerPlatformOperations` and retains the Windows implementation as its compatibility default.
- Removed Windows `user32.dll` window-hiding P/Invoke and executable-path selection logic from `ServerService`.
- Removed direct `Process.Kill(entireProcessTree: true)` fallback logic from `ServerService`; forced termination now delegates through the platform service.
- Reduced `ServerService` further while preserving the public facade, current session semantics, stream readers, startup/restart behavior, native MOD evidence, and operational-health logic.
- No intended UI, dark-theme, button, tooltip, server configuration, or runtime behavior changes.


## v0.2.15.14 FIX1 — Composition Root Namespace Compile Hotfix

- Added the missing `using PalworldManager.Models;` import to `ApplicationServiceComposition.cs`.
- Restored visibility of `AppSettings` in the new application composition root.
- Added regression-harness coverage for the composition-root namespace dependency.
- No service graph, platform abstraction, UI, MOD health, or runtime behavior changed.


## v0.2.15.14 — Application Composition & Platform Abstraction Preparation

- Added `ApplicationServiceComposition` as an explicit composition root for MystTiq's core server/MOD/diagnostics service graph.
- Removed direct construction of core server/MOD services from `MainWindow`; the window now consumes the composed graph and remains responsible for UI event wiring.
- Added `IServerSessionInspector` as the platform boundary for server-session process tree, loaded-module, descendant-process, and guarded-port inspection.
- `ServerSessionInspector` remains the Windows implementation and preserves v0.2.15.12 session/module behavior.
- `ServerService` now depends on `IServerSessionInspector` while retaining a Windows default implementation for compatibility.
- Established a clean seam for a future Linux session/process inspector without adding OS conditionals throughout server/runtime-evidence logic.
- No intended UI, theme, button, tooltip, health, MOD evidence, start/stop/restart, or server configuration behavior changes.


## v0.2.15.13 FIX2 — Regression Harness Quoting Hotfix

- Fixed PowerShell parsing in `Test-v0.2.15.13-Logic.ps1`.
- Replaced nested double-quoted text in the conflict-regression failure message with PowerShell-safe single-quoted message strings.
- No C# health logic, MOD conflict semantics, UI behavior, or server health scoring changed from FIX1.


## v0.2.15.13 FIX1 — MOD Conflict Health False-Positive Hotfix

- Fixed a server-health false positive where `No known conflict` matched a broad `Contains("Conflict")` check.
- MOD conflict health now requires either `Compatibility == Conflict` or the explicit status `Confirmed conflict`.
- Healthy MODs with `No known conflict` no longer count as confirmed server-health issues.
- Added regression-harness coverage to prevent broad conflict-string matching from returning.
- No change to genuine conflict, missing dependency, runtime error, failed, missing, or misconfigured MOD penalties.


## v0.2.15.13 — Operational Health Model & Application Composition Refinement

- Added `ModPlatformHealthService` as the single source of truth for MOD contribution to server-level health.
- Disabled, Active / Unverified, Active, Installed, and Unknown MOD states are informational/neutral and no longer reduce Overall Health.
- Enabled MOD failures, runtime errors, missing deployment, misconfiguration, state/duplicate attention, confirmed conflicts, and missing dependencies are explicit health issues.
- `DashboardIntelligenceService` now consumes `ModPlatformHealthSnapshot` instead of deriving `installed - healthy`.
- Dashboard MOD card, compact health strip, Overall Health detail, and tooltip now share the centralized MOD health interpretation.
- Preserved v0.2.15.12 FIX1 server lifecycle/process modularization and v0.2.15.10+ runtime evidence behavior.


## v0.2.15.12 FIX1 — Server Inspector Delegation Compile Hotfix

- Repaired three stale `ServerService` references left after process/session inspection was extracted to `ServerSessionInspector`.
- Cleanup now obtains descendant process IDs and guarded listening ports through the extracted inspector.
- Added a public read-only descendant-process query to `ServerSessionInspector`.
- Updated the stale v0.2.15.11 MOD-center source comment to v0.2.15.12.
- Tightened the v0.2.15.12 regression harness to detect stale extracted-helper references before release build.
- No lifecycle behavior, cleanup safety policy, runtime evidence semantics, UI styling, buttons, or tooltips changed.


## v0.2.15.12 — Server Lifecycle & Process Modularization

- Extracted PalServer process-tree, loaded-module, and guarded-port inspection into `ServerSessionInspector`.
- Extracted lifecycle-state interpretation into the pure `ServerLifecycleEvaluator`.
- Reduced `ServerService` while retaining it as the compatibility facade used by the rest of MystTiq.
- Preserved current-session/PID snapshot semantics required by native UE4SS runtime evidence.
- Preserved start, stop, restart, adoption, cleanup, update, resource-monitoring, and I/O behavior.
- Established explicit process/session boundaries that can later be placed behind Windows/Linux platform interfaces.
- No intended UI, theme, button, tooltip, configuration, or runtime-evidence semantic changes.


## v0.2.15.11 FIX1 — MOD Center Extraction Compile Hotfix

- Repaired four interpolated dialog strings in `MainWindow.ModCenter.cs` that were accidentally split across physical source lines during the modularization extraction.
- Restored the original escaped newline behavior (`\n\n`) without changing dialog text or runtime logic.
- Added regression harness checks for the extracted MOD-center dialog strings.
- No MOD architecture, runtime evidence, UI styling, or workflow behavior changed.


## v0.2.15.11 — MOD Architecture Modularization

- Added `ModCoordinator` as the application-facing orchestration boundary for inventory, verification, compatibility, recommendations, and verification export.
- Added `ModDashboardStateService` to own Dashboard projection, merge, health, and summary logic without WPF dependencies.
- Added MOD workflow/result models.
- Extracted the MOD Library/Dashboard/UI workflow from the monolithic `MainWindow.xaml.cs` into `MainWindow.ModCenter.cs`.
- MainWindow now delegates MOD workflow orchestration instead of coordinating backend services directly.
- Preserved runtime evidence, native module verification, UI behavior, button/tooltip standards, and dark theme.
- No intentional user-facing feature or file-format changes.


## v0.2.15.10 FIX1 — Release Synchronization & Harness Hotfix

- Fixed the v0.2.15.10 PowerShell regression harness by replacing the `R` helper name that collided with PowerShell's `r` / `Invoke-History` alias.
- Removed obsolete v0.2.15.9 harnesses from the active `scripts` folder.
- Synchronized `docs/index.html` and `src/PalworldManager/app.manifest` to v0.2.15.10.
- Refreshed release checklist, release notes, build/test plan, apply instructions, and source manifest.
- No native runtime module evidence or MOD verification behavior changed.


## v0.2.15.10 — Native Runtime Module Evidence

- Integrated read-only PalServer process-module evidence for native/hybrid UE4SS mods.
- Exact canonical path matching prevents collisions between mods that both use `main.dll`.
- Native module mapping provides 100% Confirmed Loaded evidence while Confirmed Running still requires functional activity.
- Runtime inspection refreshes the current ServerService session snapshot and includes the PalServer process tree.
- Enumeration failures fail open to Active / Unverified, never a false load failure.


## v0.2.15.9 FIX2 — Native UE4SS Detection & Verification

- Corrected UE4SS inventory classification using actual payload: Lua, Native, Hybrid, or generic UE4SS.
- Added non-executing PE validation and bounded printable-string analysis for native DLL capability hints.
- Native static signatures are diagnostic capability evidence only; they never count as proof that a DLL executed.
- Separated `Active / Unverified` from actual MOD attention/failure counts.
- Dashboard now reports quiet valid MODs as `awaiting runtime confirmation` instead of `need attention`.
- Preserved current-session runtime evidence requirements and observational safety.


## v0.2.15.9 FIX1 — Compile Hotfix

- Added the missing `ConfirmedRunning` member to `RuntimeEvidenceState`.
- Updated the v0.2.15.9 regression harness so PowerShell build steps are judged by exceptions rather than stale `$LASTEXITCODE` values.
- Updated the stale v0.2.15.8 source comment that caused release validation to warn.
- No runtime-verification architecture or functional-detection behavior changed.


## v0.2.15.9 — MOD Functional Verification & Capability Analysis

- Added non-destructive UE4SS capability/source analysis.
- Added Confirmed Running state based on observed current-session functional activity.
- Verification now reports MOD kind, detected runtime APIs, and expected functional proof.
- Preserved v0.2.15.8 FIX1 Active / Unverified semantics for quiet/event-driven mods.
- MystTiq remains observational and does not inject or modify third-party MOD code.


## v0.2.15.8 FIX1 — Runtime Evidence Model & Confidence Engine

- Added Confirmed Loaded, Active / Unverified, Not Loaded, Error, Disabled, and N/A runtime semantics.
- Added evidence confidence, source, matched alias, and explanation.
- Added UE4SS `enabled.txt, starting mod` loader acknowledgement detection.
- Absence of a positive signature no longer falsely means Not Loaded for a correctly deployed/enabled UE4SS mod.
- Preserved current-session evidence boundaries.


All notable public changes will be documented here.

## [0.2.15.8] - 2026-08-09

### Added
- Added `ModRuntimeEvidenceEngine` as the centralized interpreter for UE4SS/Lua positive runtime evidence.
- Added structured evidence explanations with source and matched runtime alias for verification/report diagnostics.
- Added conservative positive UE4SS signatures for Starting/Loading Lua mods and explicit loaded/initialized/registered mod messages.
- Added event-driven runtime-state synchronization so authoritative evidence updates existing MOD Library and Dashboard rows without waiting for a manual Verify All.

### Changed
- RuntimeStateService now delegates positive load-signature extraction to the shared runtime evidence engine instead of owning a single `Starting Lua mod` regex.
- UE4SS verification now consumes the same authoritative runtime state as the MOD Library and treats `LoadedByUe4ss` as positive evidence when the inventory has already resolved a valid runtime identity.
- Runtime verification details now explain whether evidence came from the unified runtime session or unified inventory state.

### Fixed
- Prevented MOD Dashboard rows such as AntiDupe and PalImportFilter from remaining **Runtime Unverified** while the MOD Library already reports them **Loaded**.
- Removed the verification timing window where runtime evidence could arrive after a Dashboard scan but before a later manual refresh.

## [0.2.15.7] - 2026-08-09

### Added
- Added `RuntimeStateService` as the authoritative, session-aware owner for MOD runtime-loaded evidence.
- Added immutable `RuntimeStateSnapshot` records with session ID, revision, timestamps, runtime log identity, loaded aliases, runtime errors, and health metadata.
- Added session-bound UE4SS log offsets so new sessions consume only newly written runtime evidence instead of inheriting historical `Starting Lua mod` lines.
- Added runtime-state diagnostics to the MOD Runtime view, including session ID, revision, loaded alias count, and last observation time.

### Changed
- MOD inventory scans now observe runtime evidence through `RuntimeStateService` and apply the shared snapshot to MOD rows.
- MOD Library refreshes no longer own a private runtime-loaded latch.
- Server session preparation starts a new runtime-state session; server exit clears the authoritative runtime state.
- Runtime verification/export continues to use the same inventory rows, which are now populated from the centralized runtime source of truth.

### Fixed
- Prevented periodic MOD Library refreshes and UE4SS log changes from erasing valid current-session loaded state.
- Prevented runtime-loaded evidence from a previous PalServer session from being carried into a new session.

### v0.2.15.7 FIX1 — Compile hotfix
- Fixed the two-argument `ModService` constructor to chain through its declared `ue4ssResolver` parameter instead of the stale pre-refactor identifier.
- Removed an unused `RuntimeStateService` field from `GenericModVerifier`, clearing the migration warnings without changing verification behavior.
- Added a regression check for the constructor-chain compile failure.

### v0.2.15.6 FIX2 — Runtime-loaded session persistence
- Fixed MOD Library runtime-loaded state reverting to **Not loaded** after UE4SS log rotation/refresh.
- Positive UE4SS load evidence is now latched for the current PalServer session and is not erased by later logs that omit startup lines.
- Session-loaded evidence resets on server exit and before a new server session, preventing stale cross-session status.
- Added regression checks for session-latch application and reset boundaries.

## [0.2.15.6 FIX1] - 2026-08-08

### Fixed
- MOD Library UE4SS/Lua `LOADED` state now refreshes dynamic runtime evidence instead of reusing a pre-start cached resolver snapshot.
- Workshop and managed packages now retain UE4SS runtime-folder aliases so `Starting Lua mod` evidence can match the actual runtime identity even when package/friendly names differ.
- The MOD Library automatically synchronizes after the 45-second startup evidence window.
- Logic regression harness now recognizes the `report.CanStart` gate implementation and no longer treats stale native `$LASTEXITCODE` values as failed PowerShell build steps.

## [0.2.14.9 FIX3] - 2026-08-06

### Changed
- Update Center row actions now use compact sizing that fits the action column.
- Update Center actions now use semantic colors for update, verify, source, install, retry/check, enable/disable, manage, and create operations.

### Fixed
- Admin Commands runtime status now recognizes common package-name and successful-load message variants.
- Admin Commands status now updates from both process output and tailed `Pal.log` content, including when MystTiq adopts an already-running server.

### v0.2.14.9 FIX1
- Added required Windows installer generation and Inno Setup bootstrap tooling.
- Installer assets now publish to `artifacts` with SHA-256 checksums.
- UE4SS runtime compatibility now refreshes from the live MOD inventory after every MOD state change.
- Removed stale hard-coded MOD compatibility guidance.

## [0.2.14.9 FIX2] - 2026-08-06

### Fixed
- Mouse-wheel routing across nested grids, lists, logs, and page-level scroll viewers.
- Diagnostics Center now scrolls correctly when the pointer is over the results grid.
- Parent pages automatically receive wheel input when a nested control reaches its scroll boundary.

## [0.2.14.9] - 2026-08-06

### Added
- Release-candidate validation for repository hygiene, XAML resources, event handlers, version consistency, dialog usage, and release documentation.
- Unified `Build.ps1 Validate` action.

### Changed
- Full release preparation now validates the source tree before build and packaging.

## [0.2.14.8] - 2026-08-06

### Added
- Central application constants for non-user-configurable timing and network defaults.
- Dedicated MainWindow lifecycle partial for startup, timer ownership, cancellation, and shutdown cleanup.
- Window-lifetime cancellation token to stop startup work cleanly when the application closes.

### Changed
- MainWindow constructor no longer contains the complete loaded/closed orchestration logic.
- Monitor, automation, and heartbeat timer subscriptions now have named handlers and are explicitly detached at shutdown.
- Startup and monitor exceptions are isolated, logged, and surfaced through the notification infrastructure without crashing the UI thread.
- Long-lived cancellation sources and disposable services are released through one predictable shutdown path.
- Repeated operational timing values now consume shared constants to reduce configuration drift.

### Safety
- No world, player, guild, base, backup, MOD, or configuration-editing behavior was intentionally changed.
- This release is focused on lifecycle reliability, maintainability, and shutdown safety.

## [0.2.14.5] - 2026-08-06

### Added
- Guided Repair Center status banner, world scan action, repair candidate summary cards, and selection controls.
- Read-only repair-plan workflow with candidate, selected, and high-risk counts.

### Changed
- World Inspector is now the single navigation entry point for world validation and repair workflows.
- Removed the redundant World Validator item from the left navigation.
- Repair Center uses shared MystTiq dark-theme cards, semantic buttons, tooltips, and compact spacing.

## [0.2.14.3] - 2026-08-06

### Added
- Central application dialog facade and expanded dialog service.
- Shared MystTiq button density variants for standard, compact, toolbar, wide, icon, and DataGrid actions.
- Formal UI standards documentation under `release-notes`.

### Changed
- Existing application dialogs now route through one central integration point while preserving current behavior.
- Historical apply notes, build/test plans, and compile hotfix notes now live under `release-notes`.
- Environment DataGrid actions now use the shared DataGrid button standard.

## [0.2.14.2] - 2026-08-05

### Added
- Central player-save discovery and validation service.
- Guarded filesystem enumeration service.
- Ordered startup coordinator with independent stage results.

### Changed
- Players, Guilds, World discovery, World Tools, and Player Recovery now share the same player-save rules.
- Volatile Palworld filesystem scans now tolerate files and folders changing during enumeration.


## [0.2.14.7] - 2026-08-06

### Added
- Diagnostics Center under Tools with read-only application, workspace, server, world, backup, MOD, transaction, and notification checks.
- Provider-based `DiagnosticsService` architecture for registering subsystem health checks.
- Weighted health score with passed, warning, and failed totals.
- JSON and text diagnostics report export.
- Redacted support-package ZIP containing diagnostics, selected configuration metadata, and recent size-limited logs.

### Changed
- Diagnostics and support artifacts are stored in the centralized diagnostics directory for installed and portable modes.
- Transaction Center read-only wording no longer embeds an obsolete version number.


## [0.2.14.6] - 2026-08-06

### Added
- Read-only Transaction Center inside World Inspector.
- Durable transaction and world-import journal discovery.
- Search, filtering, stage details, backup/report links, and rollback-availability reporting.

### Changed
- Transaction History foundation is now an active audit center.

### Safety
- Rollback execution remains disabled until the rollback framework is validated.
- Malformed transaction records are skipped and logged without interrupting history loading.

## [0.2.14.4] - 2026-08-06

### Added
- Dark-themed World Management migration wizard inside World Inspector.
- Progressive seven-step migration workflow with locked, active, and completed states.
- World Inspector tabs for Players, Guilds, Bases, World Validator, World Management, Repair Center, and Transaction History.
- Guided status banner explaining the current migration state and next required action.
- Sidebar World Validator shortcut now opens the validator inside World Inspector.

### Changed
- World Validator is now organized as a World Inspector tab rather than a separate working page.
- Existing repair preview is presented as Repair Center.
- Player, Guild, and Base tabs link to their full management pages while preserving one World Inspector workspace.
- Locked migration stages explain what must be completed before they become available.

## [0.2.14.1] - 2026-08-05

### Added
- Dedicated Workspace Manager page.
- Portable/installed mode visibility and path inspection.
- Workspace validation, folder-opening, browsing, and synchronized path saving.


## [0.2.13.2] - 2026-08-05

### Added
- Portable workspace and application path foundation.
- Automatic PalServer and SteamCMD discovery inside the portable workspace.
- Portable-local settings, logs, cache, notifications, diagnostics, and window state.

### Changed
- Portable packages now include a working directory layout and marker file.
- Installer builds now use non-portable published files.

## [0.2.13.1] - 2026-08-04

### Added
- Central version definition in `Directory.Build.props`.
- Runtime application-version service.
- Shared version reader for build and packaging scripts.
- Release-tag version validation.

### Changed
- Window title, sidebar, user agents, exports, packaging, and installer builds now consume the central version.

## [0.2.12] - 2026-08-04

### Added
- First public open-source baseline.
- Professional one-page operations dashboard.
- Compact CPU and RAM history graphs.
- Live activity ticker and notification center.
- Separate operational-state and overall-health reporting.
- Health breakdown tooltip.
- Standardized MystTiq dark-theme buttons and tooltips.
- Rounded transparent application icon.

### Fixed
- Intentionally stopped servers no longer report poor health solely because they are stopped.
- Live-save temporary files no longer interrupt player and world inspection.
- Notification bell now toggles correctly.
- Clearing the final notification closes the flyout and hides the bell immediately.

## [0.2.14.1 FIX1]
### Fixed
- Backup Center now handles Palworld-exclusive live save locks with an optional coordinated stop-backup-start workflow instead of failing after repeated copy attempts.

## v0.2.14.1 FIX2

### Fixed
- Dashboard guild/base totals now initialize on first launch.
- Players, Guilds, Bases, and guild/base recovery snapshots are preloaded automatically after startup.
- Startup world-data stages fail independently so one unavailable subsystem does not prevent the rest of the application from loading.


## v0.2.14.11
- Finalized Update Center polish
- Admin Commands refresh reliability improvements
- Scroll routing audit
- Documentation and release wrap-up completed

## v0.2.15.1

### Added
- Central `Ue4ssRuntimeResolver` as the authoritative source for UE4SS runtime and MOD-root paths.
- `Ue4ssRuntimeInfo` snapshot model for modern, legacy, active, and runtime-reported MOD roots.
- UE4SS.log parsing for `Loading mods from:` runtime verification.
- Session-cached resolution with explicit refresh and invalidation support.
- Startup diagnostics that log the selected active root, detection method, runtime-reported root, and health state.

### Changed
- v0.2.15.x development now begins from the validated v0.2.14.11 baseline.
- The resolver prefers an existing modern `Win64\ue4ss\Mods` layout, can use UE4SS runtime-log evidence, retains legacy `Win64\Mods` compatibility, and never selects the legacy root merely because it contains more mods.

### Safety
- Phase 1 performs detection and diagnostics only. It does not migrate, move, delete, enable, disable, or rewrite existing MOD files.
## v0.2.15.2

### Changed
- Migrated `ModService` and `ModScannerService` UE4SS runtime operations to `Ue4ssRuntimeResolver.GetActiveModsRoot()`.
- MOD inventory now enumerates the active UE4SS Mods Root rather than assuming `Win64\Mods`.
- ZIP install planning for recognized UE4SS Lua/DLL packages now targets the active runtime root.
- Enable/disable, `mods.txt`, `enabled.txt`, state repair, managed-path detection, and delete operations use the same resolved active root.
- MOD Dashboard folder actions use the resolver-selected active root.
- Server Doctor and UE4SS dependency checks no longer treat a legacy Mods directory alone as proof of the active runtime.

### Safety
- No automatic legacy-to-modern migration or deletion is performed in this phase. Legacy content remains untouched for Phase 3 migration support.
- Existing managed manifests can still recognize both modern and legacy UE4SS paths for identification/migration purposes, while live operations target only the active runtime root.


## v0.2.15.6

### Pre-start MOD Reconciliation & Runtime Health Hardening
- Added `ModLifecycleCoordinator` as the authoritative boundary for normal modded PalServer startup.
- Pre-start reconciliation now repairs UE4SS `enabled.txt` overrides, preserves/creates canonical `mods.txt` entries, and immediately rescans effective MOD state.
- Added a startup health gate that blocks normal modded startup when enabled files are missing, an enabled UE4SS MOD is outside the resolver-selected Active Mods Root, a state mismatch remains, duplicate logical installs are detected, or reconciliation returns filesystem/state warnings.
- Preserved **Start Without MODs** as an intentional health-gate bypass for recovery and isolation testing.
- Added `ModRepairRecommendationEngine` for deterministic operator-facing repair guidance.
- Added TXT + JSON MOD verification report export from the MOD Dashboard using existing MystTiq button/tooltip/dark-theme standards.
- Updated versioning, README roadmap, release checklist, release notes, build/test plan, apply instructions, and source manifest for v0.2.15.6.

## v0.2.15.5

### Centralized MOD Health & Identity
- Added one authoritative `ModHealthEvaluationService` used by verification and UI health presentation.
- UE4SS/Lua mods are no longer reported Healthy without matching runtime load evidence while the server is running.
- Added explicit Runtime Unverified and Misconfigured health states.
- PAK/Workshop mods use installation/enabled verification and do not require UE4SS Lua load evidence.
- Main Dashboard healthy counts now include only genuinely Healthy MODs.
- Workshop display names are resolved from MystTiq's metadata cache during inventory scans so rescans preserve names such as `PalSchema (3625280368)`.
- MOD verification summaries now distinguish healthy, runtime-unverified, misconfigured/attention, disabled, failed/missing, and unknown states.

## v0.2.15.4

### Runtime-Loaded Status & Expanded UE4SS Diagnostics
- Added active-runtime presence and UE4SS-loaded state to managed MOD inventory rows.
- Parse the latest UE4SS runtime log for `Starting Lua mod` evidence.
- UE4SS/Lua mods that are not present under the resolver-selected Active Mods Root now report `Misconfigured` rather than healthy/installed.
- Expanded MOD Runtime diagnostics with UE4SS root, Active Mods Root, Legacy Mods Root, runtime-reported root, path health, active/legacy directory counts, and loaded Lua-mod count.
- Preserved v0.2.15.3 migration semantics; no automatic migration or destructive legacy cleanup was added.

## v0.2.15.3

### UE4SS Legacy Migration & ZIP Normalization — Phase 3
- Added safe copy-first migration from the legacy `Win64\Mods` directory into the resolver-selected Active Mods Root.
- Legacy copies are retained; migration never performs destructive moves or deletes.
- Existing active-root files are preserved when their content differs, and conflicts are reported instead of overwritten.
- Known UE4SS runtime-component folders are skipped so legacy runtime components cannot replace the active UE4SS installation.
- Added a MOD Dashboard migration action using existing MystTiq warning-button and tooltip standards.
- Normalized `ue4ss\Mods\<Mod>`, `Mods\<Mod>`, and wrapped `<Mod>\Scripts\...` archive layouts into `<ActiveModsRoot>\<Mod>\...`.
- Removed the remaining generic ZIP deployment path that could recreate `Win64\Mods` for UE4SS package content.
### v0.2.15.3 FIX1
- Changed the Windows installer default directory for fresh installs to `C:\GameServers\MystTiqPalworldServer`.
- Installer and post-install launch now run elevated; the application itself continues to request administrator privileges on subsequent launches.
- Updated README installer guidance and executable naming.

