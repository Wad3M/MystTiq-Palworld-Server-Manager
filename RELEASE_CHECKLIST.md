# Release Checklist

- [ ] Update `VersionPrefix` in `Directory.Build.props`.
- [ ] Add matching `release-notes/v<version>.md`.
- [ ] Update `CHANGELOG.md`.
- [ ] Synchronize `src/PalworldManager/app.manifest` assembly identity.
- [ ] Run `./scripts/Get-ProjectVersion.ps1`.
- [ ] Clean and build `Release | x64`.
- [ ] Verify window, sidebar, executable, and export versions.
- [ ] Run `./scripts/Package-Portable.ps1`.
- [ ] Build installer when applicable.
- [ ] Verify `bin`, `obj`, `.vs`, `artifacts`, saves, logs, and credentials are not committed.
- [ ] Commit and push source.
- [ ] Create a matching `v<version>` Git tag.
- [ ] Confirm GitHub Actions succeeds.
- [ ] Verify release assets and SHA256 checksums.
