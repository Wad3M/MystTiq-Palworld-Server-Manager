# Apply v0.4.6.4 Changed Files

Preferred test method: replace the working tree with the **v0.4.6.4 FullSource** package to avoid stale source. The Changed Files package is provided for review/overlay workflows.

After applying, run `scripts\Test-v0.4.6.4-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson` or use `Update-FromDownloads.ps1` for the one-command update + complete gate.
