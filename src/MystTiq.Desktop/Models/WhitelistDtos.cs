namespace MystTiq.Desktop.Models;

public sealed class WhitelistEntryDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

// Sent to and returned from GET/PUT /players/whitelist -- the whole config is read/replaced at
// once, matching the existing Discord Bot config's GET+PUT convention (add/remove locally, one
// explicit Save persists the full list).
public sealed class WhitelistConfigDto
{
    public bool Enabled { get; set; }
    public List<WhitelistEntryDto> Entries { get; set; } = [];
}
