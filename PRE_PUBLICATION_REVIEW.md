# Pre-Publication Safety Review

Automated pattern scan of the GitHub-ready source package. This is not a substitute for manual review.

No obvious hard-coded credentials, access tokens, or private keys were detected by the automated scan.

## Manual checks still required

- Open the app once with a clean profile and verify no personal server paths are embedded in defaults.
- Confirm documentation and future screenshots contain no public IPs, Windows usernames, player identifiers, or credentials.
- Never commit `Data`, `Logs`, `Backups`, `.sav` files, `.env` files, or publish output.
- Replace every `Wad3M` placeholder after choosing the GitHub repository URL.
- Review the full GitHub Desktop changed-file list before the first commit.
