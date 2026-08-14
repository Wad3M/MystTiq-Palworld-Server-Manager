# Build / Test Plan — v0.3.0.4

## Windows gate

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.4-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

## One-command Linux deployment + acceptance

From the repository root:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -CachePasswordForSession
```

Default target is `mystroth@192.168.1.248`.

For the disposable-VM lifecycle mutation pass:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -CachePasswordForSession -Extended
```

The Linux runner must report zero FAIL entries. Review WARN entries and the timestamped raw report directory before promotion.

## Manual fallback on Linux

```bash
cd ~/mysttiq-builds/v0.3.0.4
bash ./scripts/Test-v0.3.0.4-LinuxAcceptance.sh
```

The default runner is non-destructive. `--extended` enables lifecycle mutation checks.


## One-command full Linux service acceptance

After deployment, from the extracted Linux build:

```bash
bash ./scripts/Test-v0.3.0.4-LinuxAcceptance.sh --install-current --extended
```

This may prompt once for `sudo`, installs the current extracted binary into systemd, starts it, runs the consolidated checks and saves all evidence under the timestamped report directory.
