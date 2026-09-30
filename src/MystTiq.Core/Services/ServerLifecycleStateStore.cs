// MystTiq v0.9.10.0: file reviewed for this release (2026-09-30).
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

            // v0.9.6.0: opened so that a write can still replace the file while it is being read (see Write).
            using var stream = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<PersistedServerLifecycleState>(stream, jsonOptions);
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

        // v0.9.6.0: one writer at a time (two writers shared the same .tmp file), and a replace that Windows refuses
        // because something has the file open (a virus scan, a backup, a script reading it) is retried briefly instead of
        // failing the start or stop that wrote it (seen as "Access to the path is denied" on a start).
        lock (writeGate)
        {
            var temporaryPath = statePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, jsonOptions));
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, statePath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 20)
                {
                    Thread.Sleep(25);
                }
            }
        }
    }

    private readonly object writeGate = new();
}
