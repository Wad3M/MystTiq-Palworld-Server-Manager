namespace PalworldManager.Models;

public sealed class InstallProgressInfo
{
    public string Component { get; set; } = "";
    public string Message { get; set; } = "";
    public int Percent { get; set; }
    public bool IsError { get; set; }
}
