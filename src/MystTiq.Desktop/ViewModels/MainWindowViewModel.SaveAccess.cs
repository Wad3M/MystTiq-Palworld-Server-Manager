// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
using System.Windows.Input;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.4: the Backups page's save-folder access warning and its fix (see SaveFolderAccessFix).
public sealed partial class MainWindowViewModel
{
    private bool _saveFolderReplaceable = true;
    private string _savedFolderPathVerbatim = string.Empty;
    private string _saveFolderAccessStatus = string.Empty;

    // Shown for a server on this computer whose save folder restores cannot replace.
    public bool ShowSaveFolderAccessFix => !_saveFolderReplaceable && ElevatedFirewall.CanRun(SelectedProfile) && _savedFolderPathVerbatim.Length > 0;
    public string SavedFolderPathVerbatim { get => _savedFolderPathVerbatim; private set => SetField(ref _savedFolderPathVerbatim, value); }
    public string SaveFolderAccessStatus { get => _saveFolderAccessStatus; private set => SetField(ref _saveFolderAccessStatus, value); }

    public ICommand FixSaveFolderAccessCommand { get; }

    private void ApplySaveFolderAccess(BackupInventoryDto inventory)
    {
        _saveFolderReplaceable = inventory.SaveFolderReplaceable;
        SavedFolderPathVerbatim = inventory.SavedFolderPath ?? string.Empty;
        RaisePropertyChanged(nameof(ShowSaveFolderAccessFix));
    }

    private async Task FixSaveFolderAccessAsync()
    {
        if (SelectedProfile is null || SavedFolderPathVerbatim.Length == 0) return;
        IsBusy = true;
        try
        {
            var (_, message) = await SaveFolderAccessFix.RunAsync(SavedFolderPathVerbatim);
            SaveFolderAccessStatus = message;
            await RefreshBackupsCoreAsync(SelectedProfile);
        }
        catch (Exception ex) { SaveFolderAccessStatus = ex.Message; }
        finally { IsBusy = false; }
    }
}
