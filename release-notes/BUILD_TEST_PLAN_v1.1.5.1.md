<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.5.1 Build and Test Plan

1. Static gate: `pwsh ./scripts/Test-v1.1.5.1-Logic.ps1` (the v1.1.5.1 contracts: the type check, `SafePath`, the player-ID
   check, the logic-harness scenario).
2. Full gate: `pwsh ./scripts/Test-v1.1.5.1-Logic.ps1 -RunBuild -RunLong` (builds, both harnesses, every route smoke).
3. Logic harness, "Safe paths": `SafePath.ChildOf` refuses `..\`, `../`, a nested `a\..\..\`, an absolute path, an empty
   name and `.`; `IsPlayerId` takes only 32 hexadecimal characters; Set description source, Get description and Rollback
   with a type carrying `..\` are refused and write nothing; a real type still saves; the character disposition with a
   source ID carrying `..\` is refused (Delete and Archive) and the file it pointed at is untouched.
4. Packages: `Build.ps1 Clean`, `Validate-Release.ps1 -Strict`, `Build-ConsoleProxy.ps1`, `Build.ps1 Package`,
   `Build-DockerImage.ps1 -Save`, `Build-Checksums.ps1`; the distribution check; the Docker image's `/healthz`; the
   setup's administrative extract.
5. GitHub: after the push, code scanning re-runs on `main` and the path alerts close.
