<!-- MystTiq v0.9.4.0: file reviewed for this release (2026-09-28). -->
# Publishing a MystTiq release on GitHub

This guide publishes **v0.9.4.0**; for a later version, replace the version everywhere below. The version always comes from `Directory.Build.props`, and the release workflow refuses a tag that does not match it. The accepted baseline is v0.8.25.0; the next feature milestone is v0.9.4.0.

A GitHub release is three things: a **tag** on a commit (`v0.9.4.0`), the **release page** (title and notes), and the **assets** attached to it (the ZIPs and their checksums). The repository's release workflow makes all three for you as a *draft*; nothing is public until you press **Publish release**.

## 1. Get the source onto GitHub

The v0.9.4.0 tree must be committed to `main` first. From your clone (for example `E:\Projects\MystTiq-Palworld-Server-Manager`):

```powershell
git switch main
git pull --ff-only                      # take any Dependabot or web edits first
# copy the v0.9.4.0 source over the clone (keep the clone's .git folder), then:
git add --all                           # stages new files AND the removals
git status                              # review: no bin/, obj/, artifacts/, saves, logs or secrets
git commit -m "v0.9.4.0: roles explained on disabled controls, and accessibility"
git push origin main
```

`git add --all` matters whenever a version removes files (v0.8.26.0 removed about a thousand: the legacy WPF app, its installer, old tests and pre-0.8 change lists, listed in [FILE_AUDIT_v0.8.26.0.md](FILE_AUDIT_v0.8.26.0.md)); only `--all` records removals.

Wait for the **Build** workflow on that commit to go green (Actions tab). Do not tag a commit whose build failed.

## 2. Check it locally (recommended)

On Windows with PowerShell 7 and the .NET 10 SDK, from the repository root:

```powershell
pwsh ./scripts/Validate-Release.ps1 -Strict
pwsh ./Build.ps1 Package                # builds, then packages Windows and Linux ZIPs + SHA256SUMS.txt
```

`Build.ps1 Release` does the same after the full release gate (about 50 minutes). The packages land in `artifacts/`:

- `MystTiqPalworldServer_v0.9.4.0_Windows-x64.zip`: the Avalonia desktop with the headless service in `headless\`, self-contained (no .NET install needed).
- `MystTiqPalworldServer_v0.9.4.0_Linux-x64.zip`: the same for Linux (after extracting: `chmod +x MystTiq.Desktop headless/mysttiq-server`).
- `SHA256SUMS.txt`: their hashes.

Extract the Windows ZIP into an empty folder, start `MystTiq.Desktop.exe`, and check the title bar shows v0.9.4.0 and it connects. Keep live server data outside the extracted folder. The packager never overwrites an existing ZIP: delete old ones from `artifacts/` first if you rebuild.

For the Windows native console helper (`headless\native\MystTiqConsoleProxy.dll`), run `pwsh ./scripts/Build-ConsoleProxy.ps1` first (needs the MSVC x64 build tools); the release workflow always does.

## 3. Tag and let GitHub build the release (preferred)

```powershell
git switch main
git pull --ff-only
git tag -a v0.9.4.0 -m "MystTiq v0.9.4.0"
git push origin v0.9.4.0
```

Pushing the tag starts the **Release** workflow (`.github/workflows/release.yml`). It:

1. checks the tag equals `v` + the version in `Directory.Build.props`, and that `release-notes/v0.9.4.0.md` exists;
2. builds the native console helper and the Windows package (`Package-GitHubRelease.ps1 -Runtime win-x64 -RequireNativeProxy`);
3. archives the tagged source as `MystTiqPalworldServer_v0.9.4.0_FullSource.zip`;
4. writes `SHA256SUMS.txt`;
5. creates a **draft prerelease** named "MystTiq v0.9.4.0" with the release notes as its text and those files attached.

If it fails, fix the cause on `main`; if the tag itself is fine, rerun it from **Actions → Release → Run workflow** with the tag `v0.9.4.0`. Never move or re-use a published tag.

## 4. Review and publish

1. Open **Releases**; the draft is at the top.
2. Check the title, the notes, the target commit and the three assets (Windows ZIP, FullSource ZIP, SHA256SUMS.txt).
3. Optionally download the Windows ZIP and compare its hash: `Get-FileHash .\MystTiqPalworldServer_v0.9.4.0_Windows-x64.zip -Algorithm SHA256`.
4. Keep **Set as a pre-release** ticked (v0.x is before the v1.0 stability gate).
5. Press **Publish release**. Then check the download links work.

## Manual alternative (no workflow)

Use this only if you are not pushing a tag (do not do both for the same version).

1. Build the ZIPs locally (step 2) and make the source ZIP: `git archive --format=zip --output=artifacts/MystTiqPalworldServer_v0.9.4.0_FullSource.zip HEAD`.
2. Recompute `artifacts/SHA256SUMS.txt` so it covers all the ZIPs: `pwsh ./scripts/Build-Checksums.ps1 -Include '*.zip'`.
3. On GitHub: **Releases → Draft a new release**, choose or create the tag `v0.9.4.0` on the reviewed commit, title **MystTiq v0.9.4.0 — Roles Explained, and Accessibility**, paste [`release-notes/v0.9.4.0.md`](../../release-notes/v0.9.4.0.md).
4. Attach the Windows ZIP, the FullSource ZIP and `SHA256SUMS.txt`, tick **Set as a pre-release**, **Save draft**, review, then **Publish release**.

## What not to upload

- The Linux ZIP, until Linux desktop acceptance is complete (window, clipboard and tray pass; file pickers and other desktops are still open, see the [roadmap](../roadmap/PRODUCT_ROADMAP.md)). The release workflow publishes Windows only on purpose.
- Private checkpoint archives, logs, test reports, server saves, `artifacts/runtime-smoke` or anything with tokens or passwords.

Before publishing, also go through the [pre-publication review](pre-publication-review.md) and the [release checklist](../../RELEASE_CHECKLIST.md). Any Nexus Mods coordination (their API policy asks public apps to contact support with a test build) is a separate owner action.
