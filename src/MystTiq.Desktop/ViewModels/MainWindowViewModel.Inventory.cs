// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
using System.Collections.ObjectModel;
using System.Windows.Input;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v1.0.4.0 (roadmap S-1, S-2; owner decision D-2: guarded edits only): the selected player's main inventory from the world
// save, one stack removed or one added, each previewed, confirmed (MainWindow's Click handlers) and applied with a fresh,
// checked safety backup while the server is stopped.
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<InventorySlotDto> InventorySlots { get; } = [];
    private InventorySlotDto? _selectedInventorySlot;
    public InventorySlotDto? SelectedInventorySlot { get => _selectedInventorySlot; set => SetField(ref _selectedInventorySlot, value); }
    private string _inventoryStatus = string.Empty;
    public string InventoryStatus { get => _inventoryStatus; private set => SetField(ref _inventoryStatus, value); }
    public ICommand LoadInventoryCommand { get; }

    private async Task LoadInventoryAsync()
    {
        if (SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { InventoryStatus = "Select a player first."; return; }
        IsBusy = true;
        InventoryStatus = "Reading the world save…";
        try
        {
            var view = await _api.GetInventoryAsync(SelectedProfile, playerId, BearerToken);
            InventorySlots.Clear();
            foreach (var slot in view.Slots) InventorySlots.Add(slot);
            InventoryStatus = view.Detail;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task<InventoryEditPreviewDto?> PreviewInventoryEditAsync(string action)
    {
        if (SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { InventoryStatus = "Select a player first."; return null; }
        var request = new InventoryEditRequestDto { PlayerId = playerId, Action = action };
        if (action == "remove")
        {
            if (SelectedInventorySlot is not { } slot) { InventoryStatus = "Select a stack in the inventory first."; return null; }
            request.ItemId = slot.ItemId; request.SlotIndex = slot.SlotIndex; request.Count = slot.Count;
        }
        else
        {
            if (SelectedGameId?.Id is not { Length: > 0 } id) { InventoryStatus = "Pick an item in the list above first."; return null; }
            request.ItemId = id;
            request.Count = int.TryParse(GameIdAmountText, out var n) && n > 0 ? n : 1;
        }
        IsBusy = true;
        try
        {
            var preview = await _api.PreviewInventoryEditAsync(SelectedProfile, request, BearerToken);
            if (!preview.CanApply) InventoryStatus = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; return null; }
        finally { IsBusy = false; }
    }

    public async Task ApplyInventoryEditAsync(string token)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyInventoryEditAsync(SelectedProfile, token, BearerToken);
            InventoryStatus = result.Message;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; }
        finally { IsBusy = false; }
        await LoadInventoryAsync();
    }
}
