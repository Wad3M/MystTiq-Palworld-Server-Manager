<!-- MystTiq v1.1.1.3: file reviewed for this release (2026-10-19). -->
# MystTiq v1.1.1.3 Checkpoint: Dependency Updates

## What changed

The four Dependabot updates, folded in as you asked: Avalonia 12.1.4 (the desktop's UI framework), Discord.Net 3.20.2
(the Discord bot), the Windows service host 10.0.13, and the release workflow's download-artifact v8. Once this is
pushed, Dependabot closes its four pull requests and deletes their branches by itself.

**Roadmap reviewed with you:** every Built item is Done; X-1, B-5 and the carried evidence except E-10 (native translation
review) are rejected; F-19 (why a server stopped) and F-20 (start with Windows) are planned for v1.1.2.0; the 127.0.0.1
investigation is not planned; Melly's Xbox character and Spiral stay.

## Verification

- **Full gate** `scripts\Test-v1.1.1.3-Logic.ps1 -RunBuild -RunLong` (VM at 192.168.1.122): 431 / 432 passed, 1 skipped
  (the system-wide unit: no passwordless sudo). The one failure was the Linux desktop-session check, whose file probe
  hung eight hours on an `ssh` that never saw the end of its session (the remote command had finished); it was stopped and
  the check rerun on its own: **passed** (the real window on the VM's XFCE desktop, all four roles). Both harnesses pass on
  Avalonia 12.1.4.
- **Packages:** the Windows ZIP, the Linux ZIP and the Docker image (`/healthz` answered v1.1.1.3 with TLS and sign-in
  on), no `createdump`, all in `SHA256SUMS.txt`. **Linux VM:** v1.1.1.3 installed in the home folder (`~/MystTiq/current`),
  the production unit untouched. **Static gate:** passed after the README and roadmap updates. **Validate-Release
  -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
