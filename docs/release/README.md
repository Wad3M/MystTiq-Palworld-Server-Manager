<!-- MystTiq v1.0.0.4: file reviewed for this release (2026-10-05). -->
# Publishing a MystTiq release on GitHub

This guide publishes **v1.0.0.4**; for a later version, replace the version everywhere below. The version always comes from `Directory.Build.props`, and the release workflow refuses a tag that does not match it. v1.0.0.0 is the accepted baseline.

A GitHub release is three things: a **tag** on a commit (`v1.0.0.4`), the **release page** (title and notes), and the **assets** attached to it (the ZIPs and their checksums). You create and push the tag. The release workflow builds the assets and creates a *draft release* for review (a v0.x tag makes a draft prerelease). The pushed tag and source commit are already public in a public repository; the release page and attached downloads remain draft until you press **Publish release**.

Code signing through SignPath: see [CODE_SIGNING.md](CODE_SIGNING.md). With the SignPath variables set, the workflow waits for you to approve the signing request in SignPath before it makes the draft.

## 1. Get the source onto GitHub

The v1.0.0.4 tree must be committed to `main` first. From your clone (for example `E:\Projects\MystTiq-Palworld-Server-Manager`):

```powershell
git switch main
git pull --ff-only                      # take any Dependabot or web edits first
# apply the reviewed v1.0.0.4 changes, including explicit obsolete-file removals, then:
git add --all                           # stages new files AND the removals
git status                              # review: no bin/, obj/, artifacts/, saves, logs or secrets
git commit -m "v1.0.0.4: restore checked by the world's day"
git push origin main
```

`git add --all` matters whenever a version removes files (v0.8.26.0 removed about a thousand: the legacy WPF app, its installer, old tests and pre-0.8 change lists, listed in [FILE_AUDIT_v0.8.26.0.md](FILE_AUDIT_v0.8.26.0.md)); `--all` stages removals that have already been made. Copying a ZIP over an existing checkout does not remove obsolete files; use the reviewed removal list and inspect the diff.

Wait for the **Build** workflow on that commit to go green (Actions tab). Do not tag a commit whose build failed.

## 2. Check it locally (recommended)

On Windows with PowerShell 7 and the .NET 10 SDK, from the repository root:

```powershell
pwsh ./scripts/Validate-Release.ps1 -Strict
pwsh ./Build.ps1 Package                # builds, then packages Windows and Linux ZIPs + SHA256SUMS.txt
```

`Build.ps1 Release` does the same after the full release gate (about 50 minutes). The packages land in `artifacts/`:

- `MystTiqPalworldServer_v1.0.0.4_Windows-x64.zip`: the Avalonia desktop with the headless service in `headless\`, self-contained (no .NET install needed).
- `MystTiqPalworldServer_v1.0.0.4_Linux-x64.zip`: the same for Linux (after extracting: `chmod +x MystTiq.Desktop headless/mysttiq-server`).
- `SHA256SUMS.txt`: their hashes.

Extract the Windows ZIP into an empty folder, start `MystTiq.Desktop.exe`, and check the title bar shows v1.0.0.4 and it connects. Keep live server data outside the extracted folder. The packager never overwrites an existing ZIP: delete old ones from `artifacts/` first if you rebuild.

For the Windows native console helper (`headless\native\MystTiqConsoleProxy.dll`), run `pwsh ./scripts/Build-ConsoleProxy.ps1` first (needs the MSVC x64 build tools); the release workflow always does.

## 3. Tag and let GitHub build the release (preferred)

```powershell
git switch main
git pull --ff-only
git tag -a v1.0.0.4 -m "MystTiq v1.0.0.4"
git push origin v1.0.0.4
```

Pushing the tag starts the **Release** workflow (`.github/workflows/release.yml`). It:

1. checks the tag equals `v` + the version in `Directory.Build.props`, and that `release-notes/v1.0.0.4.md` exists;
2. builds the native console helper and the Windows package (`Package-GitHubRelease.ps1 -Runtime win-x64 -RequireNativeProxy`);
3. archives the tagged source as `MystTiqPalworldServer_v1.0.0.4_FullSource.zip`;
4. writes `SHA256SUMS.txt`;
5. creates a **draft release** (a prerelease for a v0.x tag) named "MystTiq v1.0.0.4" with the release notes as its text and those files attached.

If a source change is needed, commit the fix and prepare a new version/tag; changing `main` does not change an existing tag. For a transient failure that needs no source change, rerun from **Actions → Release → Run workflow** with the tag `v1.0.0.4`. Never move or re-use a published tag.

## 4. Review and publish

1. Open **Releases**; the draft is at the top.
2. Check the title, the notes, the target commit and the three assets (Windows ZIP, FullSource ZIP, SHA256SUMS.txt).
3. Optionally download the Windows ZIP and compare its hash: `Get-FileHash .\MystTiqPalworldServer_v1.0.0.4_Windows-x64.zip -Algorithm SHA256`.
4. Leave **Set as a pre-release** unticked and **Set as the latest release** ticked (v1.0 and later are full releases; MystTiq's own update check counts only those).
5. Press **Publish release**. Then check the download links work.

## Manual alternative (no workflow)

Prefer the workflow above. For manual assets, coordinate with the repository maintainer so the tag-triggered workflow and manual upload do not both edit the same release.

1. Build the ZIPs locally (step 2) and make the source ZIP: `git archive --format=zip --output=artifacts/MystTiqPalworldServer_v1.0.0.4_FullSource.zip HEAD`.
2. Recompute `artifacts/SHA256SUMS.txt` so it covers all the ZIPs: `pwsh ./scripts/Build-Checksums.ps1 -Include '*.zip'`.
3. On GitHub: **Releases → Draft a new release**, choose or create the tag `v1.0.0.4` on the reviewed commit, title **MystTiq v1.0.0.4**, paste [`release-notes/v1.0.0.4.md`](../../release-notes/v1.0.0.4.md).
4. Attach the Windows ZIP, the FullSource ZIP and `SHA256SUMS.txt`, leave **Set as a pre-release** unticked, **Save draft**, review, then **Publish release**.

## What not to upload

- The Linux ZIP, until Linux desktop acceptance is complete (window, clipboard and tray pass; file pickers and other desktops are still open, see the [roadmap](../roadmap/PRODUCT_ROADMAP.md)). The release workflow publishes Windows only on purpose.
- Private checkpoint archives, logs, test reports, server saves, `artifacts/runtime-smoke` or anything with tokens or passwords.

Before publishing, also go through the [pre-publication review](pre-publication-review.md) and the [release checklist](../../RELEASE_CHECKLIST.md). Any Nexus Mods coordination is a separate owner action.
