// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
namespace MystTiq.Desktop.Models;

// v1.0.4.0 (roadmap S-1, S-2): a player's main inventory as the world save holds it, and the guarded edit's preview and result.
public sealed class InventorySlotDto
{
    public int SlotIndex { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool HasOwnRecord { get; set; }
    // Data: the slot, the item's id and how many, as stored.
    public string RowVerbatim => $"{SlotIndex,3}  {ItemId} × {Count}";
    public string RecordText => HasOwnRecord ? "Has its own record (cannot be removed here)" : string.Empty;
}

public sealed class InventoryViewDto
{
    public bool Available { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public int Capacity { get; set; }
    public List<InventorySlotDto> Slots { get; set; } = [];
    public string Detail { get; set; } = string.Empty;
}

public sealed class InventoryEditRequestDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
    public int? SlotIndex { get; set; }
}

public sealed class InventoryEditPreviewDto
{
    public bool CanApply { get; set; }
    public string? Token { get; set; }
    public List<string> Findings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresUtc { get; set; }
}

public sealed class InventoryEditResultDto
{
    public bool Success { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public string? JournalPath { get; set; }
    public string Message { get; set; } = string.Empty;
}
