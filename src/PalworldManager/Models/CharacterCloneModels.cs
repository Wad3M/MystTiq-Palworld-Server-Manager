namespace PalworldManager.Models;

public sealed class CharacterCloneOptions
{
    public bool Inventory { get; set; } = true;
    public bool Equipment { get; set; } = true;
    public bool Technology { get; set; } = true;
    public bool LevelAndStats { get; set; }
    public bool Appearance { get; set; }
    public bool FastTravel { get; set; }
    public bool MapDiscovery { get; set; }
    public bool Paldeck { get; set; }
    public bool Palbox { get; set; }

    public IReadOnlyList<string> SelectedCategories()
    {
        var values = new List<string>();
        if (Inventory) values.Add("Inventory");
        if (Equipment) values.Add("Equipment");
        if (Technology) values.Add("Technology");
        if (LevelAndStats) values.Add("Level and Stats");
        if (Appearance) values.Add("Appearance");
        if (FastTravel) values.Add("Fast Travel");
        if (MapDiscovery) values.Add("Map Discovery");
        if (Paldeck) values.Add("Paldeck");
        if (Palbox) values.Add("Palbox");
        return values;
    }
}

public sealed class CharacterCloneCategoryPreview
{
    public string Category { get; set; } = "";
    public int SourceNodes { get; set; }
    public int DestinationNodes { get; set; }
    public bool CanCopy => SourceNodes > 0 && DestinationNodes > 0;
    public string Status => CanCopy ? "Ready" : SourceNodes == 0 ? "Not found in source" : "Not found in destination";
}

public sealed class CharacterClonePreview
{
    public PlayerRow SourcePlayer { get; set; } = default!;
    public PlayerRow DestinationPlayer { get; set; } = default!;
    public string SourceSavePath { get; set; } = "";
    public string DestinationSavePath { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public bool ServerMustBeStopped { get; set; }
    public CharacterCloneOptions Options { get; set; } = new();
    public List<CharacterCloneCategoryPreview> Categories { get; set; } = [];
    public List<string> Findings { get; set; } = [];
    public string SourceHash { get; set; } = "";
    public string DestinationHash { get; set; } = "";

    public bool CanApply => CodecAvailable && !ServerMustBeStopped && File.Exists(SourceSavePath) && File.Exists(DestinationSavePath) && Categories.Any(x => x.CanCopy);
}

public sealed class CharacterCloneResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public int NodesCopied { get; set; }
    public bool VerificationPassed { get; set; }
    public List<string> CategoriesCopied { get; set; } = [];
    public List<string> Messages { get; set; } = [];
}
