namespace MystTiq.Desktop.Models;

// v0.7.94.0: starter kits, mirroring HeadlessKitService's wire shapes. The whole kit config is read and
// replaced at once (same convention as the whitelist): edit locally, one explicit Save persists it all.
public sealed class KitEntryDto
{
    public string Type { get; set; } = "Item";
    public string Id { get; set; } = string.Empty;
    public int Amount { get; set; } = 1;
}

public sealed class KitDefinitionDto : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = string.Empty;
    private List<KitEntryDto> _entries = [];

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
    public List<KitEntryDto> Entries { get => _entries; set { _entries = value; Raise(nameof(Entries)); } }

    // The kit list is bound to this; the editor changes Name/Entries live, so the label must follow.
    public string DisplayText => string.IsNullOrWhiteSpace(Name) ? "(unnamed kit)" : $"{Name}  ·  {Entries.Count} entr{(Entries.Count == 1 ? "y" : "ies")}";
    public override string ToString() => DisplayText;

    private void Raise(string property)
    {
        PropertyChanged?.Invoke(this, new(property));
        PropertyChanged?.Invoke(this, new(nameof(DisplayText)));
    }
}

public sealed class KitConfigDto
{
    public bool AutoGiftEnabled { get; set; }
    public string AutoGiftKitId { get; set; } = string.Empty;
    public DateTimeOffset? AutoGiftEnabledAtUtc { get; set; }
    public List<KitDefinitionDto> Kits { get; set; } = [];
}

public sealed class KitClaimDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string KitId { get; set; } = string.Empty;
    public DateTimeOffset ClaimedAtUtc { get; set; }
    public string Trigger { get; set; } = string.Empty;

    public string DisplayText => $"{PlayerName}  ·  {KitId}  ·  {Trigger}  ·  {ClaimedAtUtc.ToLocalTime():g}";
}

public sealed class KitProviderStatusDto
{
    public bool CanDeliver { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class KitSnapshotDto
{
    public KitConfigDto Config { get; set; } = new();
    public KitProviderStatusDto Provider { get; set; } = new();
    public List<KitClaimDto> Claims { get; set; } = [];
}

public sealed class KitSaveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public KitConfigDto Config { get; set; } = new();
    public List<string> Errors { get; set; } = [];
}

public sealed class KitGiveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = [];
    public string Response { get; set; } = string.Empty;
}

public sealed class KitCommandResultDto
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// v0.8.3.0: the Give Item picker's catalogue (HeadlessGameIdCatalogService): item and Pal ids seen in the world save,
// used in kits, or given before.
public sealed class GameIdCatalogEntryDto
{
    public string Kind { get; set; } = "Item";
    public string Id { get; set; } = string.Empty;
    public int WorldCount { get; set; }
    public bool InKit { get; set; }
    public bool GivenBefore { get; set; }
    public bool AlphaSeen { get; set; }
    // v0.8.13.0: the game's English name when known, and whether only the game's name tables list this id.
    public string? Name { get; set; }
    public bool InGameFiles { get; set; }

    public bool IsPal => Kind.Equals("Pal", StringComparison.OrdinalIgnoreCase);

    // Where the id comes from, so it is clear why it is listed (e.g. "Pal Sphere (PalSphere) · Item · in world ×757 · in a kit").
    public string DisplayText
    {
        get
        {
            var parts = new List<string> { IsPal ? "Pal" : "Item" };
            if (WorldCount > 0) parts.Add(IsPal ? $"in world ×{WorldCount}{(AlphaSeen ? " (incl. alpha)" : string.Empty)}" : $"in world ×{WorldCount}");
            if (InKit) parts.Add("in a kit");
            if (GivenBefore) parts.Add("given before");
            if (InGameFiles && WorldCount == 0 && !InKit && !GivenBefore) parts.Add("not yet seen on this server");
            var title = string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} ({Id})";
            return $"{title}  ·  {string.Join(" · ", parts)}";
        }
    }

    public override string ToString() => DisplayText;
}

public sealed class GameIdCatalogDto
{
    public bool WorldAvailable { get; set; }
    public string? WorldId { get; set; }
    public DateTimeOffset? WorldDecodedUtc { get; set; }
    public int ItemCount { get; set; }
    public int PalCount { get; set; }
    public List<GameIdCatalogEntryDto> Entries { get; set; } = [];
    public string Detail { get; set; } = string.Empty;
    // v0.8.13.0: whether display names came from the game files, and why not when they did not.
    public bool NamesAvailable { get; set; }
    public string NamesDetail { get; set; } = string.Empty;
}