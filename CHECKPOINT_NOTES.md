<!-- MystTiq v1.0.0.0: file reviewed for this release (2026-09-30). -->
# MystTiq v1.0.0.0 Checkpoint: MystTiq 1.0

You asked to publish v0.9.10.0 as version 1.0, set it as the accepted baseline, and keep the open items in the internal docs. Signing through SignPath comes with v1.0.1.0.

## What changed

- **Version:** 1.0.0.0. There are no application code changes from v0.9.10.0.
- **Full release:** the release workflow now makes a draft full release for v1 and later. v0.x tags stay prereleases.
  - This matters to the app: from 1.0 on, MystTiq's update check counts only full releases, so a 1.x published as a
    prerelease would never be offered.
- **Public docs:** the README, the site and the release notes present the stable release.
  - Removed from them: the known limitations, the site's "What still needs verification" section and the draft
    translation notes.
  - The accepted baseline is now v1.0.0.0 (it was v0.8.25.0). From the next version on, the upgrade tests also start from this checkpoint.
- **Internal docs keep the open items:** `docs/architecture/v1.0.0.0-stable-release.md` and the roadmap's "Open after
  1.0" section.
  - Translations have no native-speaker review.
  - Live checks not yet done: Pal delivery, kits, refused gives, live map markers, chat teleport, Discord and email.
  - No screen-reader pass.
  - Linux acceptance is incomplete, and the Linux download is not published.
  - The code limits noted in v0.9.10.0.

- **Code signing, ready but not active:**
  - When the SignPath repository variables exist, the release workflow uploads the Windows app to SignPath, waits for your approval, checks each MystTiq file's signature and zips the signed files. Without them it packages unsigned.
  - All seven MystTiq binaries now carry the product name and version, including a new version resource on the native helper.
  - Setup steps: `docs/release/CODE_SIGNING.md`. Policy pages: `CODE_SIGNING_POLICY.md` and `PRIVACY.md`.
- **v1.1 planned:** a MOD browser connected to Nexus Mods and other online repositories.

## Verification

- **Full gate:** 246/246 (`gate1000-full.txt`), including the frozen v0.9.10.0 gate.
  - Four Linux VM checks were skipped: the VM stopped answering SSH during the run.
- **Static gate:** 200/200. **Clean and strict validation:** 0 errors, 0 warnings. **Distribution check:** 4/4.
- **Signing path:** staging, then zipping from the staged folder, tested locally. All seven MystTiq binaries read "MystTiq
  Palworld Server Manager" 1.0.0.0. The SignPath step itself can't run until the project exists.
- **Windows, live:** `/healthz` reports 1.0.0.0. One helper was reused after restarting the app.
- **The source ZIP on its own:** after this checkpoint, its FullSource ZIP was extracted into an empty folder and its own
  gate run there (`archive1000-gate.txt`, next to the ZIP).

## Publishing

With your go-ahead, I pushed `main` (v0.9.0.0 through v1.0.0.0) and the `v1.0.0.0` tag. The release workflow builds the
Windows ZIP, the source ZIP and the checksums, and creates an unsigned **draft release**. On GitHub, check that **Set as a
pre-release** is unticked and **Set as the latest release** is ticked, then press **Publish release**.