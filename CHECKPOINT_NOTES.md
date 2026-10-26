<!-- MystTiq v1.1.5.1: file reviewed for this release (2026-10-26). -->
# MystTiq v1.1.5.1 Checkpoint: Safer File Paths

## What changed

- **MOD files stay in their folders.** A MOD's type must be PAK, UE4SS or NATIVE, and the snapshot, description-cache,
  PAK and UE4SS paths built from a MOD's type and package must stay inside their folders (`SafePath.ChildOf`).
- **The character move names only real players.** Keep, Archive and Delete of the old save take only a 32-character
  player ID, and their files stay in the world's Players folder.
- GitHub code scanning alert #24 is the bearer-token check itself (false positive, left as it is).

## Verification

- **Full gate** `scripts\Test-v1.1.5.1-Logic.ps1 -RunBuild -RunLong` (2026-10-26): 458 / 459 passed, 6 skipped (the Linux
  VM, removed from the owed list by the owner). The one failure was "Strict validation passes": two example strings
  still named v1.1.5.0 (the bug-report form's placeholder and the release workflow's manual-run hint). Both updated to
  v1.1.5.1; `Validate-Release.ps1 -Strict` then passed on the clean tree (0 errors, 0 warnings) and the static gate
  398 / 398. Logic harness: every scenario, including "Safe paths (v1.1.5.1, CodeQL)"; desktop harness: every check.
- **Packages:** Windows ZIP, portable ZIP (with `portable.txt` and the console proxy), setup `.msi` (administrative
  extract: 577 files, installed.txt, the console proxy, no portable.txt), Linux ZIP and Docker image (`/healthz` answered
  1.1.5.1 over TLS); all in `SHA256SUMS.txt`; the distribution check passed. **VirusTotal:** these files are new; the
  owner scans them if wanted (v1.1.5.0's setup 0/49 and portable 0/62; only the service changed).

## Still owed

See the roadmap's "Outstanding for v1.1.5.0": SignPath (O-4, applied 2026-10-25, waiting for approval), the v1.1.5.0
release build (O-9: re-run it, or let v1.1.5.1 be the next published release), the SecureAge false positive (O-10), the
Dependabot pull request (O-11) and the translation review (O-8). On GitHub: dismiss code scanning alert #24 as a false
positive once the re-scan has closed the path alerts.
