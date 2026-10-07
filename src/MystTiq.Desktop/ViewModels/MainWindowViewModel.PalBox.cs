// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Collections.ObjectModel;
using System.Windows.Input;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v1.0.6.0 (roadmap S-3; owner decisions D-2 guarded edits only, D-7 add and remove a Pal): the selected player's Pal box
// from the world save, one Pal added (a copy of one of that species already in a Pal box) or removed, each previewed,
// confirmed (MainWindow's Click handlers) and applied with a fresh, checked safety backup while the server is stopped.
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<PalBoxPalDto> PalBoxPals { get; } = [];
    public ObservableCollection<string> PalBoxSpecies { get; } = [];
    private PalBoxPalDto? _selectedPalBoxPal;
    public PalBoxPalDto? SelectedPalBoxPal { get => _selectedPalBoxPal; set => SetField(ref _selectedPalBoxPal, value); }
    private string? _selectedPalBoxSpecies;
    public string? SelectedPalBoxSpecies { get => _selectedPalBoxSpecies; set => SetField(ref _selectedPalBoxSpecies, value); }
    private string _palBoxStatus = string.Empty;
    public string PalBoxStatus { get => _palBoxStatus; private set => SetField(ref _palBoxStatus, value); }
    public ICommand LoadPalBoxCommand { get; private set; } = null!;

    private void InitializePalBox() => LoadPalBoxCommand = new AsyncCommand(LoadPalBoxAsync, () => !IsBusy && SelectedPlayerRecord is not null);

    private async Task LoadPalBoxAsync()
    {
        if (SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { PalBoxStatus = "Select a player first."; return; }
        IsBusy = true;
        PalBoxStatus = "Reading the world save…";
        try
        {
            var view = await _api.GetPalBoxAsync(SelectedProfile, playerId, BearerToken);
            PalBoxPals.Clear();
            foreach (var pal in view.Pals) PalBoxPals.Add(pal);
            var keep = SelectedPalBoxSpecies;
            PalBoxSpecies.Clear();
            foreach (var species in view.AddableSpecies) PalBoxSpecies.Add(species);
            SelectedPalBoxSpecies = keep is not null && PalBoxSpecies.Contains(keep) ? keep : PalBoxSpecies.FirstOrDefault();
            PalBoxStatus = view.Detail;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task<PalBoxEditPreviewDto?> PreviewPalBoxEditAsync(string action)
    {
        if (SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { PalBoxStatus = "Select a player first."; return null; }
        var request = new PalBoxEditRequestDto { PlayerId = playerId, Action = action };
        if (action == "remove")
        {
            if (SelectedPalBoxPal is not { } pal) { PalBoxStatus = "Select a Pal in the box first."; return null; }
            request.InstanceId = pal.InstanceId;
        }
        else
        {
            if (SelectedPalBoxSpecies is not { Length: > 0 } species) { PalBoxStatus = "Pick the Pal to add first."; return null; }
            request.Species = species;
        }
        IsBusy = true;
        try
        {
            var preview = await _api.PreviewPalBoxEditAsync(SelectedProfile, request, BearerToken);
            if (!preview.CanApply) PalBoxStatus = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; return null; }
        finally { IsBusy = false; }
    }

    public async Task ApplyPalBoxEditAsync(string token)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyPalBoxEditAsync(SelectedProfile, token, BearerToken);
            PalBoxStatus = result.Message;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; }
        finally { IsBusy = false; }
        await LoadPalBoxAsync();
    }
}
