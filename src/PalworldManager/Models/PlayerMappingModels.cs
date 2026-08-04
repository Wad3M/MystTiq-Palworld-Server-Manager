namespace PalworldManager.Models;
public enum PlayerMappingMethod { ExactGuid, PlatformId, ExactName, HostMigration, Manual, Unmatched }
public sealed class PlayerMappingRecord
{
    public string SourcePlayerGuid { get; set; } = "";
    public string DestinationPlayerGuid { get; set; } = "";
    public PlayerMappingMethod Method { get; set; }
    public double Confidence { get; set; }
    public bool Confirmed { get; set; }
    public string Explanation { get; set; } = "";
}
