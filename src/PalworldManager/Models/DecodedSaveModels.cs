namespace PalworldManager.Models;

public sealed class SaveCodecResult
{
    public bool Success { get; set; }
    public string SourcePath { get; set; } = "";
    public string JsonPath { get; set; } = "";
    public string CodecName { get; set; } = "";
    public string CodecVersion { get; set; } = "";
    public List<string> Diagnostics { get; set; } = [];
}

public sealed class DecodedWorldSave
{
    public string SourcePath { get; set; } = "";
    public string JsonPath { get; set; } = "";
    public JsonDocument? Document { get; set; }
}
