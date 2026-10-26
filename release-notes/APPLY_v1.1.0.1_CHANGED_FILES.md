<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.0.1 Changed Files

- `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs`: the start-up check for a port in use
  (`PortAcceptsConnectionsAsync`) cancels its connect attempt after 750 ms and waits for it, instead of abandoning it.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: the check on a closed and a listening port, counting socket errors
  left unobserved after it returns.
- Tests: `scripts/Test-v1.1.0.1-Logic.ps1`.
