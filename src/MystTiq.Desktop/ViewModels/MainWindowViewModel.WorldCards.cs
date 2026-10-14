// MystTiq v1.0.9.0: file reviewed for this release (2026-10-12).
using System.Windows.Input;
using Avalonia.Threading;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.5 (asked 2026-10-05): distinct Bases and Guilds pages. Bases are places (location, workers, owner); guilds are people
// (roster, leader, bases). Each links to the other, and a base opens on the map.
public sealed partial class MainWindowViewModel
{
    private string _basesSummaryText = string.Empty;
    private string _guildsSummaryText = string.Empty;

    public string BasesSummaryText { get => _basesSummaryText; private set => SetField(ref _basesSummaryText, value); }
    public string GuildsSummaryText { get => _guildsSummaryText; private set => SetField(ref _guildsSummaryText, value); }
    public bool HasPlayerGuildWarnings => World.PlayerGuildWarnings.Count > 0;

    public ICommand ShowSelectedBaseOnMapCommand { get; }
    public ICommand OpenSelectedBaseGuildCommand { get; }
    public ICommand OpenGuildBaseCommand { get; }

    // The explorer read, shaped for the two pages.
    private void ApplyWorldCards(PlayerGuildSnapshotDto snapshot)
    {
        var bases = WorldExplorerCards.Bases(snapshot.Guilds, snapshot.BaseLocations, snapshot.PalLocations);
        World.ExplorerBases.Clear();
        foreach (var card in bases) World.ExplorerBases.Add(card);
        foreach (var guild in World.ExplorerGuilds)
        {
            guild.Roster = WorldExplorerCards.Roster(guild, snapshot.Players);
            guild.BaseCards = bases.Where(b => string.Equals(b.GuildId, guild.GuildId, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var workers = bases.Sum(b => b.Workers.Count);
        BasesSummaryText = $"{bases.Count} bases · {bases.Count(b => b.HasLocation)} on the map · {workers} Pals working";
        var online = snapshot.Players.Count(p => p.Online);
        GuildsSummaryText = $"{snapshot.Guilds.Count} guilds · {snapshot.Guilds.Sum(g => g.MemberCount)} members · {online} online";
        RaisePropertyChanged(nameof(HasPlayerGuildWarnings));
    }

    private void ShowSelectedBaseOnMap()
    {
        var baseId = World.SelectedExplorerBase?.BaseId;
        if (baseId is null) return;
        Map.ShowBasesOnMap = true;
        if (!Map.IsWorldMapExpanded) Map.IsWorldMapExpanded = true;
        Navigate(nameof(NavigationPage.Map));
        // After the page has switched and laid out its map.
        Dispatcher.UIThread.Post(() => World.ZoomToBase(baseId), DispatcherPriority.Background);
    }

    private void OpenSelectedBaseGuild()
    {
        var guildId = World.SelectedExplorerBase?.GuildId;
        if (guildId is null) return;
        World.GuildSearchText = string.Empty;
        World.SelectedExplorerGuild = World.ExplorerGuilds.FirstOrDefault(g => string.Equals(g.GuildId, guildId, StringComparison.OrdinalIgnoreCase));
        Navigate(nameof(NavigationPage.Guilds));
    }

    private void OpenGuildBase(string? baseId)
    {
        if (string.IsNullOrWhiteSpace(baseId)) return;
        World.BaseSearchText = string.Empty;
        World.SelectedExplorerBase = World.ExplorerBases.FirstOrDefault(b => string.Equals(b.BaseId, baseId, StringComparison.OrdinalIgnoreCase));
        Navigate(nameof(NavigationPage.Bases));
    }
}
