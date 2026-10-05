<!-- MystTiq v1.0.0.2: file reviewed for this release (2026-10-05). -->
# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

## What is signed

The Windows download's own MystTiq binaries: `MystTiq.Desktop.exe`, `MystTiq.Desktop.dll`, `MystTiq.Core.dll`, and in
`headless` `mysttiq-server.exe`, `mysttiq-server.dll`, `MystTiq.Core.dll` and `native/MystTiqConsoleProxy.dll`. They are
built from this repository's source by the release workflow (`.github/workflows/release.yml`) on GitHub-hosted runners
and signed only after a release is approved. Third-party libraries in the download (for example Avalonia and the .NET
runtime) are shipped as their authors publish them and are not re-signed by this project.

## Team roles

- Authors (committers): [Wad3M](https://github.com/Wad3M)
- Reviewers: [Wad3M](https://github.com/Wad3M)
- Approvers (release signing): [Wad3M](https://github.com/Wad3M)

All team members use multi-factor authentication for GitHub and SignPath.

## Privacy

See the [privacy policy](PRIVACY.md).
