# Contributing

Thank you for helping improve MystTiq Palworld Server Manager.

## Before opening an issue

- Search existing issues.
- Remove passwords, tokens, public IP addresses, private player identifiers, save files, and personal logs.
- Include the application version, Windows version, action performed, expected behavior, actual behavior, and exact error text.

## Development workflow

1. Fork the repository.
2. Create a focused branch, such as `fix/notification-toggle`.
3. Keep changes small and preserve the established dark theme, button standards, tooltip standards, responsive layouts, semantic colors, and existing architecture.
4. Do not redesign unrelated pages or introduce unrequested features.
5. Update `Directory.Build.props` and the applicable documentation when the change is versioned.
6. Place release notes, build test plans, compile hotfix notes, and apply instructions under `release-notes/`.
7. Run the standard build sequence from the repository root:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

8. Test the affected workflow with the server stopped and running where applicable.
9. Submit a pull request explaining the change, risks, and test results.

## Build and release tools

- `Build.ps1` is the supported root entry point.
- `scripts/Build-Release.ps1` orchestrates validation and release assets.
- `scripts/Build-Installer.ps1` supports Inno Setup 6 and 7 discovery through PATH, registry, environment, standard locations, or `-ISCC`.
- `scripts/Build-Checksums.ps1` creates and verifies `artifacts/SHA256SUMS.txt`.
- Generated `artifacts`, `bin`, and `obj` directories must never be committed.

## Code expectations

- Use nullable reference types correctly.
- Avoid blocking the WPF UI thread.
- Validate paths and handle files disappearing during live Palworld saves.
- Back up world data before destructive operations.
- Do not introduce new direct world-write paths without a validated transaction and rollback design.


## Release Workflow
Follow the MystTiq workflow: Clean → Validate → All before opening a PR.
