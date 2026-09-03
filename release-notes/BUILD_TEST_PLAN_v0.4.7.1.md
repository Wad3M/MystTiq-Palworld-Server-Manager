# v0.4.7.1 Build/Test Plan

Use `Update-FromDownloads.ps1` or run the complete gate manually. Promotion requires 0 validation errors/warnings, all v0.4.7.1 logic tests, Windows/Linux headless and Avalonia builds, runtime smoke, and manual Dashboard/Server Setup acceptance.

Manual acceptance must verify card navigation/actions, Server Setup action disabled states/reasons, programmatic navigation highlight, Start/Stop/Restart/Backup/Doctor behavior, hidden PalServer window, live console and telemetry updates.
