<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# Code signing with SignPath (setup)

Requested 2026-09-30: sign the Windows download through SignPath. The release workflow is ready; it signs as soon as the
repository variables below exist. Until then it packages unsigned, as before. Everything here needs the repository
owner's accounts, so it is a checklist for you rather than something automation can do.

## 1. Apply to SignPath Foundation (free for open source)

Apply at <https://signpath.org/apply> for `Wad3M/MystTiq-Palworld-Server-Manager`. What the Foundation checks:

- an OSI license without commercial dual-licensing (MIT: yes);
- the project is maintained and already released, and signs only binaries built from its own source by an automated
  build (the GitHub Actions release workflow);
- a code signing policy page naming the roles (authors, reviewers, approvers) and a privacy statement:
  `CODE_SIGNING_POLICY.md` and `PRIVACY.md` (review the names in them; every member needs MFA on GitHub and SignPath);
- consistent metadata on every signed binary: product name "MystTiq Palworld Server Manager" and the release's version
  (set in `Directory.Build.props` and, for the native helper, `native/MystTiqConsoleProxy/MystTiqConsoleProxy.rc`);
- a manual approval for every release signing.

To decide with them: the download also contains third-party libraries that their authors do not Authenticode-sign
(Avalonia, Discord.Net, MicroCom.Runtime, Tmds.DBus.Protocol). The Foundation's terms ask projects to try to get
upstream binaries signed; ask whether they want those left unsigned, signed under this project, or handled otherwise.

## 2. Set up the SignPath project (after approval)

In SignPath.io:

1. Add the predefined trusted build system **GitHub.com** to the organization and install the SignPath GitHub App on the
   repository.
2. Create the project (for example slug `mysttiq`) linked to that trusted build system.
3. Add an artifact configuration (slug `windows-app`) with the contents of `.signpath/artifact-configuration.xml`.
4. Add a signing policy (for example slug `release-signing`) with the Foundation's certificate and manual approval.
5. Create an API token for a CI user with the submitter role on that project.

## 3. Connect the repository

In GitHub, Settings → Secrets and variables → Actions:

- secret `SIGNPATH_API_TOKEN`: the API token;
- variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`,
  `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`.

## 4. Release

Push the tag as usual. The workflow stages the Windows app, uploads it, and submits the signing request. Approve it in
SignPath; the workflow waits up to 24 hours. It then checks that each MystTiq binary has a valid signature, makes the ZIP
from the signed files, and creates the draft release. A tag pushed before step 3 gives an unsigned release. v1.0.0.0 was published unsigned
(2026-09-30); the first signed release is planned as v1.0.1, tagged once steps 1–3 are done.
