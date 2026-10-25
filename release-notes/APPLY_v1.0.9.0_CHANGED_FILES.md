<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-25). -->
# v1.0.9.0 Changed Files

- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: `ChooseLocalConnectionCommand` and
  `ChooseRemoteConnectionCommand` removed (their methods stay; the tab commands call them).
- `src/MystTiq.Desktop/ViewModels/MapViewModel.cs`: `ToggleWorldMapCommand` removed.
- `docs/roadmap/PRODUCT_ROADMAP.md`: D-11, the backlog section (T-1, B-4, B-3, B-2, T-2), and S-3's original row marked
  as moved.
- Tests: `scripts/Test-v1.0.9.0-Logic.ps1` (generated from the v1.0.8.9 gate) with the command reachability check.
