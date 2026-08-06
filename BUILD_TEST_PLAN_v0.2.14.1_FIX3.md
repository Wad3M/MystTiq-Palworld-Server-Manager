# Build & Test Plan — v0.2.14.1 FIX3

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Notification diagnostics

1. Open **Notifications**.
2. Select **Run Self-Test**.
3. Confirm five items are created: Information, Success, Warning, Critical, and Pinned.
4. Confirm the bell appears, the unread badge updates, and the flyout opens automatically.
5. Click the bell twice and confirm open/close toggle behavior.
6. Select **Mark All Read** and confirm the unread badge disappears while the bell remains because notifications still exist.
7. Use the flyout **Clear** action and confirm the pinned test survives.
8. Select **Clear Tests** and confirm all diagnostic items are removed.
9. When no other notifications remain, confirm the flyout closes and the bell disappears immediately.
10. Confirm ordinary notifications are not removed by **Clear Tests**.

## Regression

Validate startup world initialization, coordinated live backup, Dashboard, Workspace, Players, Guilds, Bases, and World Inspector.
