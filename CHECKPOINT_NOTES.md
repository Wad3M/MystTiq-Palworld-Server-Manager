<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-22). -->
# MystTiq v1.1.5.0 Checkpoint: Resizable Window, Simple Mode, Installer and Portable

## What changed

- **A window you can size (V-1).** Drag any edge or corner; it remembers its size, position and maximized state.
- **Simple and Advanced (V-2).** A switch in the title bar (and Settings) hides the specialist pages for everyday use.
  Advanced, the default, shows everything as before.
- **Portable (W-2).** `..._Windows-x64-Portable.zip`: everything stays in its `data` folder, nothing goes to Windows.
- **Windows setup (W-1).** `..._Windows-x64.msi`: Program Files, Start-menu shortcut, uninstaller that keeps your data.
  It needs administrator rights, so I have not run it on your PC; I checked its tables and did an administrative
  extract. It is not code-signed, so SmartScreen will ask. Please scan it on VirusTotal before you publish.

## Verification

- **Full gate** `scripts\Test-v1.1.5.0-Logic.ps1 -RunBuild -RunLong`: 449 / 449 passed, 6 skipped: the Linux VM
  (192.168.1.122) answered ping but not SSH, so its checks did not run and v1.1.5.0 was not copied to it. The first run
  was cut off when the session ended; a second failed 53 route smokes because the published app had been cleaned away
  before the run (my sequencing); republished, the third passed. Desktop harness: 865 checks (resize grips and placement,
  every page at 1680, 2560 and 3840 px, Simple and Advanced, portable folders, update kinds); logic harness all scenarios.
- **Packages:** Windows ZIP, portable ZIP (with `portable.txt`), setup `.msi`, Linux ZIP and Docker image (`/healthz`
  answered v1.1.5.0 with TLS and sign-in on); no `createdump`; all in `SHA256SUMS.txt`. **VirusTotal** (owner uploaded,
  2026-10-22): the setup 0/60 with a clean sandbox run that installed to Program Files\MystTiq; the portable ZIP 0/63,
  the generic HTTP-communication label (its own helper on 127.0.0.1) and the known SecureAge false positive on the stock
  Avalonia.Dialogs.dll. **Setup:** tables read (shortcuts, the service actions and conditions) and an administrative
  extract (577 files, installed.txt present, no portable.txt); not installed on the owner's PC (needs administrator
  rights). **Portable:** its helper started on its own configuration in `data\service` and refused `service-install`.
  **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed. **Static gate:** 388 / 388.
