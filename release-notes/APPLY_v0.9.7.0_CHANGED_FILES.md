<!-- MystTiq v1.0.0.5: file reviewed for this release (2026-10-05). -->
# v0.9.7.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.7.0.
- `src/MystTiq.Desktop/MystTiq.Desktop.csproj`: Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter and
  Avalonia.Themes.Fluent 12.1.3 (were 11.3.*), pinned.
- `scripts/Testing/MystTiq.ArtworkHarness/MystTiq.ArtworkHarness.csproj`,
  `scripts/Testing/MystTiq.RemoteSignInHarness/MystTiq.RemoteSignInHarness.csproj`: Avalonia.Headless 12.1.3.
- `src/MystTiq.Desktop/MainWindow.axaml`: `TextBox.Watermark` → `PlaceholderText` (62), `SystemDecorations` →
  `WindowDecorations`. `Views/TrayReminderToast.axaml`: `WindowDecorations`.
- `src/MystTiq.Desktop/MainWindow.axaml.cs`: `using Avalonia.Input.Platform` for the clipboard's text methods.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`, `scripts/Testing/MystTiq.RemoteSignInHarness/Program.cs`: the
  app's `NavigationPage` aliased; `PlaceholderText`, `WindowDecorations`, `TryGetTextAsync`.
- `scripts/Test-v0.9.7.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.7.0.
- Docs: `docs/architecture/v0.9.7.0-avalonia-12.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` and its index, `docs/release/README.md`,
  `docs/release/GITHUB_PRESENTATION.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`, the issue
  templates.