<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.0.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 1.0.0.0.
- `.github/workflows/release.yml`: a v0.x tag makes a draft prerelease; v1 and later a draft full release.
- `README.md`, `docs/index.html`: the stable release; open items and verification notes moved to the roadmap.
- `release-notes/v1.0.0.0.md`, this file, `release-notes/BUILD_TEST_PLAN_v1.0.0.0.md`.
- `docs/architecture/v1.0.0.0-stable-release.md`, `docs/roadmap/PRODUCT_ROADMAP.md`: what 1.0 contains and what stays
  open after it.
- `docs/release/README.md`, `RELEASE_CHECKLIST.md`, `.github/ISSUE_TEMPLATE/bug_report.yml`, `CHANGELOG.md`.
- Code signing through SignPath: `.github/workflows/release.yml` (stage, upload, sign, verify, package when the SignPath
  variables are set); `scripts/Package-GitHubRelease.ps1` (`-StageOnly`, `-FromFolder`); `.signpath/artifact-configuration.xml`;
  product name, company, copyright and a commit-free product version in `Directory.Build.props`; version information for
  the native helper (`native/MystTiqConsoleProxy/MystTiqConsoleProxy.rc`, `scripts/Build-ConsoleProxy.ps1`);
  `CODE_SIGNING_POLICY.md`, `PRIVACY.md`, `docs/release/CODE_SIGNING.md`.
- Roadmap: v1.1 MOD browser (planned).
- `scripts/Test-v1.0.0.0-Logic.ps1` (new gate). No application behaviour changes.
