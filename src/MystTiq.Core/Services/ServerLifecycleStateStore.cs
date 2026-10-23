// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
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
        // v1.0.7.0 (roadmap Q-5): the retry stays (Windows reports a file another program holds as "access denied" on a
        // replace, so that is retried too), but a failure that outlasts it now says what it was and what to check instead of
        // a bare "Access to the path is denied" failing the start or stop.
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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < WriteAttempts)
                {
                    Thread.Sleep(25);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new IOException(FailureMessage(statePath, ex), ex);
                }
            }
        }
    }

    private readonly object writeGate = new();

    public const int WriteAttempts = 20;

    /// <summary>v1.0.7.0 (roadmap Q-5): why the state file could not be saved, in words an owner can act on.</summary>
    public static string FailureMessage(string path, Exception cause) => cause switch
    {
        UnauthorizedAccessException when File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0 =>
            $"MystTiq could not save the server's state file ({path}): the file is read-only. Clear its read-only setting.",
        UnauthorizedAccessException =>
            $"MystTiq could not save the server's state file ({path}) for 0.5 s: access was denied. Another program may be holding it (an antivirus scan, a backup or sync tool), or this account may not be allowed to change that folder.",
        _ =>
            $"MystTiq could not save the server's state file ({path}) for 0.5 s: {cause.Message} Another program may be holding it (an antivirus scan, a backup or sync tool)."
    };
}
