// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.Models;

// v1.0.6.0 (roadmap S-3): a player's Pal box as the world save holds it, and the guarded add/remove's preview and result.
public sealed class PalBoxPalDto
{
    public int SlotIndex { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Nickname { get; set; }
    public string Gender { get; set; } = string.Empty;
    // Data: the place in the box, the Pal's id (species), its level and the name a player gave it.
    public string RowVerbatim => $"{SlotIndex,3}  {Species}  Lv {Level}{(string.IsNullOrWhiteSpace(Nickname) ? string.Empty : $"  \"{Nickname}\"")}";
}

public sealed class PalBoxViewDto
{
    public bool Available { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public int Capacity { get; set; }
    public List<PalBoxPalDto> Pals { get; set; } = [];
    public List<string> AddableSpecies { get; set; } = [];
    public string Detail { get; set; } = string.Empty;
}

public sealed class PalBoxEditRequestDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Species { get; set; }
    public string? InstanceId { get; set; }
}

public sealed class PalBoxEditPreviewDto
{
    public bool CanApply { get; set; }
    public string? Token { get; set; }
    public List<string> Findings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresUtc { get; set; }
}

public sealed class PalBoxEditResultDto
{
    public bool Success { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public string? JournalPath { get; set; }
    public string Message { get; set; } = string.Empty;
}
