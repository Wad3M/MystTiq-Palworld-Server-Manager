<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# Contributing

Thank you for helping improve MystTiq Palworld Server Manager.

## Before opening an issue

- Search existing issues.
- Remove passwords, tokens, public IP addresses, private player identifiers, save files, and personal logs.
- Include the application version, operating system (and Linux desktop environment where relevant), action performed, expected behavior, actual behavior, and exact error text.

## Development workflow

1. Fork the repository.
2. Create a focused branch, such as `fix/notification-toggle`.
3. Keep changes small and preserve all supported appearance modes, button standards, tooltip standards, responsive layouts, semantic colors, and existing architecture.
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
- `scripts/Package-GitHubRelease.ps1` builds the self-contained Windows (`-Runtime win-x64`) and Linux (`-Runtime linux-x64`) downloads.
- `docs/release/README.md` is the step-by-step guide to publishing a GitHub release.
- `scripts/Build-Checksums.ps1` creates and verifies `artifacts/SHA256SUMS.txt`.
- Generated `artifacts`, `bin`, and `obj` directories must never be committed.

## Code expectations

- Use nullable reference types correctly.
- Avoid blocking the Avalonia UI thread; long work belongs in the headless service or on a background task.
- Validate paths and handle files disappearing during live Palworld saves.
- Back up world data before destructive operations.
- Do not introduce new direct world-write paths without a validated transaction and rollback design.


## Release Workflow
Follow the MystTiq workflow: Clean → Validate → All before opening a PR.

## Translation feedback

All 11 non-English translations are drafts. Native-speaker feedback is welcome, including small corrections.

1. Open a translation-feedback issue and name the language, app version and page/dialog.
2. Include the current wording, your suggested wording and the intended meaning. A cropped screenshot helps with context.
3. Preserve message placeholders such as `{0}` and `{1}` in a proposed catalog change. Do not translate user-entered names or paths.
4. Check natural phrasing, plurals, terminology, text expansion and accessibility labels. For Chinese, Japanese and Korean, mention the operating system and font if glyphs are missing.

Catalogs are in `src/MystTiq.Desktop/Assets/i18n/`; the [text inventory](docs/i18n/UI_TEXT_INVENTORY.md) lists the window's texts, and [TRANSLATION_REVIEW.md](docs/i18n/TRANSLATION_REVIEW.md) explains how to export a review sheet for a whole language (`scripts/Export-MystTiqTranslationReview.ps1`).
