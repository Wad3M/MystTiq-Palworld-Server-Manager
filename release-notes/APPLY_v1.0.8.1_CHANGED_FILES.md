<!-- MystTiq v1.0.9.1: file reviewed for this release (2026-10-12). -->
# v1.0.8.1 Changed Files

- `src/MystTiq.Desktop/ViewModels/HostPageViewModel.cs` (new): the HOST page, from the former `MainWindowViewModel.Host.cs`,
  `.HostHistory.cs` and `.Bandwidth.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/DashboardViewModel.cs` (new): the Dashboard's card texts, recent activity and the
  addresses line (the addresses part of `MainWindowViewModel.Addresses.cs`).
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `SelectedProfile` and `ActiveTab`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `.CommandRoles.cs`: the pages created and used; the role table
  names `Host.SaveNetworkPolicyCommand` and `Host.SaveResourcePolicyCommand`.
- `src/MystTiq.Desktop/MainWindow.axaml`: the HOST page binds `Host.*`, the Dashboard `Dashboard.*`.
- Tests: `scripts/Test-v1.0.8.1-Logic.ps1` (generated from the v1.0.8.0 gate; the HOST and Dashboard contracts read the
  new files) and the ArtworkHarness reading `vm.Host`.
