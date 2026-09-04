using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// A small, real slice of the roadmap's "notification routing matrix": Webhook is actually
// implemented; Discord/Email are typed stubs that log "not implemented" rather than silently
// dropping, future-proofing the schema without pretending they work.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel { Desktop, Webhook, Discord, Email }

public sealed record NotificationChannelConfig(NotificationChannel Channel, bool Enabled, string? TargetUrl);

public sealed class NotificationChannelConfiguration
{
    public List<NotificationChannelConfig> Channels { get; init; } =
    [
        new(NotificationChannel.Desktop, true, null),
        new(NotificationChannel.Webhook, false, null),
        new(NotificationChannel.Discord, false, null),
        new(NotificationChannel.Email, false, null)
    ];
}

public sealed record NotificationTemplate(string Id, string TitleFormat, string MessageFormat, string DefaultSeverity);

public sealed class HeadlessNotificationRoutingService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly HeadlessActivityLogService activity;
    private readonly object gate = new();
    private readonly string channelsPath;
    private readonly string templatesPath;
    private NotificationChannelConfiguration channels;
    private List<NotificationTemplate> templates;

    public HeadlessNotificationRoutingService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.activity = activity;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "notifications");
        Directory.CreateDirectory(root);
        channelsPath = Path.Combine(root, "channels.json");
        templatesPath = Path.Combine(root, "templates.json");
        channels = LoadChannels();
        templates = LoadTemplates();
    }

    public NotificationChannelConfiguration GetChannels() { lock (gate) return channels; }

    public NotificationChannelConfiguration SaveChannels(NotificationChannelConfiguration updated)
    {
        lock (gate)
        {
            channels = updated;
            Persist(channelsPath, channels);
            return channels;
        }
    }

    public IReadOnlyList<NotificationTemplate> GetTemplates() { lock (gate) return templates; }

    public IReadOnlyList<NotificationTemplate> SaveTemplates(List<NotificationTemplate> updated)
    {
        lock (gate)
        {
            templates = updated;
            Persist(templatesPath, templates);
            return templates;
        }
    }

    public NotificationTemplate? ResolveTemplate(string id)
    {
        lock (gate) return templates.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public static string Apply(string format, IReadOnlyDictionary<string, string> placeholders)
    {
        var result = format;
        foreach (var (key, value) in placeholders) result = result.Replace("{" + key + "}", value);
        return result;
    }

    // Fire-and-forget from HeadlessNotificationService.Create -- a routing failure must never
    // block or fail the underlying (already-persisted) Desktop notification.
    public void Dispatch(string severity, string title, string message)
    {
        List<NotificationChannelConfig> enabled;
        lock (gate) enabled = channels.Channels.Where(c => c.Enabled && c.Channel != NotificationChannel.Desktop).ToList();
        if (enabled.Count == 0) return;
        _ = Task.Run(() => DispatchAsync(enabled, severity, title, message));
    }

    private async Task DispatchAsync(List<NotificationChannelConfig> enabled, string severity, string title, string message)
    {
        foreach (var channel in enabled)
        {
            try
            {
                switch (channel.Channel)
                {
                    case NotificationChannel.Webhook:
                        await DispatchWebhookAsync(channel, severity, title, message);
                        break;
                    case NotificationChannel.Discord:
                    case NotificationChannel.Email:
                        activity.Record("Warning", "Notifications", $"{channel.Channel} channel not implemented", $"Enabled but dispatch is not yet supported for this channel.");
                        break;
                }
            }
            catch (Exception ex)
            {
                activity.Record("Warning", "Notifications", $"{channel.Channel} dispatch failed", ex.Message);
            }
        }
    }

    private async Task DispatchWebhookAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.TargetUrl)) return;
        var payload = new { severity, title, message, createdUtc = DateTimeOffset.UtcNow };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await HttpClient.PostAsJsonAsync(channel.TargetUrl, payload);
                if (response.IsSuccessStatusCode) return;
            }
            catch when (attempt == 0) { /* retry once */ }
        }
        activity.Record("Warning", "Notifications", "Webhook dispatch failed after retry", channel.TargetUrl);
    }

    private NotificationChannelConfiguration LoadChannels()
    {
        try { return File.Exists(channelsPath) ? JsonSerializer.Deserialize<NotificationChannelConfiguration>(File.ReadAllText(channelsPath)) ?? new() : new(); }
        catch { return new(); }
    }

    private List<NotificationTemplate> LoadTemplates()
    {
        try { return File.Exists(templatesPath) ? JsonSerializer.Deserialize<List<NotificationTemplate>>(File.ReadAllText(templatesPath)) ?? [] : []; }
        catch { return []; }
    }

    private static void Persist<T>(string path, T value)
    {
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, path, true);
    }
}
