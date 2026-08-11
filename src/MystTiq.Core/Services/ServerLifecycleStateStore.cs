using System.Text.Json;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed class ServerLifecycleStateStore
{
    private readonly string statePath;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public ServerLifecycleStateStore(string runtimeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
        Directory.CreateDirectory(runtimeRoot);
        statePath = Path.Combine(runtimeRoot, "lifecycle-state.json");
    }

    public string StatePath => statePath;

    public PersistedServerLifecycleState? Read()
    {
        try
        {
            if (!File.Exists(statePath))
                return null;

            return JsonSerializer.Deserialize<PersistedServerLifecycleState>(
                File.ReadAllText(statePath),
                jsonOptions);
        }
        catch
        {
            // A damaged state file must never prevent server inspection/control.
            return null;
        }
    }

    public void Write(PersistedServerLifecycleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);

        var temporaryPath = statePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, jsonOptions));
        File.Move(temporaryPath, statePath, overwrite: true);
    }
}
