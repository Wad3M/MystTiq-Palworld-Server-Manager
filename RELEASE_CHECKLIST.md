# MystTiq Release Checklist

This is the active release checklist for MystTiq Palworld Server Manager. Historical version-specific acceptance criteria remain under [`docs/history/`](docs/history/).

## Current release state

- **Official validated Windows baseline:** v0.2.16.4
- **Official Linux/headless baseline:** v0.3.0.1 FIX1
- **Current development candidate:** v0.3.0.2 — Linux Service & Automatic Recovery Foundation
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

## v0.3.0.2 Linux service acceptance

- [ ] `service-status` reports NotInstalled before installation.
- [ ] `service-install` requires root.
- [ ] install copies the headless host to `/opt/mysttiq/bin/mysttiq-server`.
- [ ] install creates `mysttiq-palworld.service`.
- [ ] systemd daemon reload succeeds.
- [ ] unit is enabled for boot startup.
- [ ] service remains stopped unless `--start-now` is requested or `systemctl start` is run.
- [ ] active service starts/adopts PalServer and reaches UDP 8211 readiness.
- [ ] `journalctl -u mysttiq-palworld` contains MystTiq supervisor output.
- [ ] forced PalServer crash is automatically recovered within the configured recovery budget.
- [ ] graceful `systemctl stop` leaves no PalServer process.
- [ ] reboot starts MystTiq and PalServer automatically while unit is enabled.
- [ ] uninstall stops/disables/removes the unit cleanly.
- [ ] Windows WPF compile/regression remains clean.
- [ ] Ubuntu Server 24.04.4 LTS remains documented as the primary Linux reference.

## Promotion gate

Promote v0.3.0.2 only after the Windows regression/build gate, Linux publish, complete systemd acceptance, automatic-recovery test, graceful-stop test, and boot-start test pass on the disposable Ubuntu VM.
