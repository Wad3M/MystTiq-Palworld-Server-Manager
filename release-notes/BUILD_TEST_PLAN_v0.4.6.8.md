# v0.4.6.8 Build/Test Plan

Run `Update-FromDownloads.ps1` or the complete current release gate. Promotion requires 0 validation errors/warnings, all v0.4.6.8 logic tests, Windows/Linux headless and Avalonia builds, runtime smoke, packaging, and GUI acceptance.

Specific regression: Tray Lifecycle logic must recognize Safe Exit, Force Exit, Exit GUI Only and existing lifecycle controls. GUI restoration reference/roadmap files must be packaged and the roadmap must enforce the full architecture trace and BACKEND REQUIRED mutation rule.
