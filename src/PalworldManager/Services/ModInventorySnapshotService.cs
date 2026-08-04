using System.Diagnostics;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ModInventorySnapshotService
{
    private readonly ModService mods;
    private readonly EnvironmentService environment;
    private readonly object gate = new();
    private ModInventorySnapshot? current;
    private long generation;

    public ModInventorySnapshotService(ModService mods, EnvironmentService environment)
    {
        this.mods = mods;
        this.environment = environment;
    }

    public ModInventorySnapshot Current(string trigger = "Library scan", bool force = false)
    {
        lock (gate)
        {
            if (!force && current is not null) return current;
            var timer = Stopwatch.StartNew();
            var installed = mods.Scan();
            var local = environment.ScanLocalMods();
            timer.Stop();
            current = new ModInventorySnapshot
            {
                ScannedAt = DateTime.Now,
                Duration = timer.Elapsed,
                Mods = installed,
                LocalMods = local,
                Generation = ++generation,
                Trigger = trigger
            };
            return current;
        }
    }

    public void Invalidate() { lock (gate) current = null; }
}
