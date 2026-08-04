namespace PalworldManager.Models;

public sealed class ModInventorySnapshot
{
    public DateTime ScannedAt { get; init; } = DateTime.Now;
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ModRow> Mods { get; init; } = [];
    public IReadOnlyList<LocalModRow> LocalMods { get; init; } = [];
    public long Generation { get; init; }
    public string Trigger { get; init; } = "Library scan";
}
