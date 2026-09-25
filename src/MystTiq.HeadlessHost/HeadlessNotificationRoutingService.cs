using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// A small, real slice of the roadmap's "notification routing matrix": Webhook, Discord (a plain
// Discord webhook URL, v0.6.17.0), and Email (v0.7.70.0, plain SMTP submission) are all
// implemented.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel { Desktop, Webhook, Discord, Email }

// v0.7.70.0: TargetUrl stays the Webhook/Discord field (both are plain HTTPS POST endpoints); the
// Smtp*/Email* fields are Email-only, all optional with a safe default (blank/disabled unless
// explicitly configured), consistent with every existing 3-arg NotificationChannelConfig
// construction below still compiling unchanged. Plaintext SmtpPassword in channels.json matches
// this codebase's own established local-secret convention (RCON's AdminPassword in
// PalWorldSettings.ini, bearer tokens in plain files) -- protected by OS file permissions on the
// server's own disk, not by application-level encryption, same threat model as those.
public sealed record NotificationChannelConfig(
    NotificationChannel Channel,
    bool Enabled,
    string? TargetUrl,
    string? SmtpHost = null,
    int SmtpPort = 587,
    bool SmtpUseSsl = true,
    string? SmtpUsername = null,
    string? SmtpPassword = null,
    string? EmailFrom = null,
    string? EmailTo = null);

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

// v0.8.4.0: pausing outside delivery (Discord, email, webhooks) while notifications still appear on the Notifications
// page. The v0.7.111.0 alert mute silences alerts entirely; this is the lighter option the roadmap asked for: keep the
// record in MystTiq, stop the pings. Stored apart from channels.json so saving the channel list (which replaces it
// whole) can never clear or set a pause by accident.
public sealed record NotificationDeliveryState(DateTimeOffset? PausedUntilUtc, IReadOnlyList<string> ExternalChannels, string Detail);
public sealed record NotificationDeliveryPauseRequest(int Minutes);
public sealed record NotificationDeliveryPause(DateTimeOffset? PausedUntilUtc);

public static class NotificationDeliveryPolicy
{
    public static bool IsPaused(DateTimeOffset? pausedUntilUtc, DateTimeOffset now) => pausedUntilUtc is { } until && until > now;
}

public sealed class HeadlessNotificationRoutingService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly HeadlessActivityLogService activity;
    private readonly object gate = new();
    private readonly string channelsPath;
    private readonly string templatesPath;
    private readonly string deliveryPath;
    private DateTimeOffset? deliveryPausedUntilUtc;
    private NotificationChannelConfiguration channels;
    private List<NotificationTemplate> templates;

    public HeadlessNotificationRoutingService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.activity = activity;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "notifications");
        Directory.CreateDirectory(root);
        channelsPath = Path.Combine(root, "channels.json");
        templatesPath = Path.Combine(root, "templates.json");
        deliveryPath = Path.Combine(root, "delivery.json");
        channels = LoadChannels();
        deliveryPausedUntilUtc = LoadDeliveryPause();
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

    public NotificationDeliveryState GetDeliveryState()
    {
        List<string> external;
        DateTimeOffset? until;
        lock (gate)
        {
            external = channels.Channels.Where(c => c.Enabled && c.Channel != NotificationChannel.Desktop).Select(c => c.Channel.ToString()).ToList();
            until = NotificationDeliveryPolicy.IsPaused(deliveryPausedUntilUtc, DateTimeOffset.UtcNow) ? deliveryPausedUntilUtc : null;
        }
        var routes = external.Count == 0 ? "no outside channel is switched on" : string.Join(", ", external);
        var detail = until is { } u
            ? $"Outside delivery is paused until {u:yyyy-MM-dd HH:mm} UTC: notifications still appear on the Notifications page, but nothing is sent to {routes}. Notifications from the pause are not sent afterwards."
            : $"Outside delivery is on ({routes}).";
        return new(until, external, detail);
    }

    // v0.8.4.0: minutes <= 0 resumes; the end time is set on the host's clock and capped like the alert mute.
    public NotificationDeliveryState PauseDelivery(int minutes)
    {
        lock (gate)
        {
            deliveryPausedUntilUtc = AlertMutePolicy.MuteUntil(minutes, DateTimeOffset.UtcNow);
            Persist(deliveryPath, new NotificationDeliveryPause(deliveryPausedUntilUtc));
        }
        activity.Record("Information", "Notifications", minutes <= 0 ? "Outside delivery resumed" : "Outside delivery paused",
            minutes <= 0 ? "Discord, email and webhook delivery is on again." : $"Until {deliveryPausedUntilUtc:yyyy-MM-dd HH:mm} UTC.");
        return GetDeliveryState();
    }

    // Fire-and-forget from HeadlessNotificationService.Create -- a routing failure must never
    // block or fail the underlying (already-persisted) Desktop notification.
    public void Dispatch(string severity, string title, string message)
    {
        List<NotificationChannelConfig> enabled;
        lock (gate) enabled = channels.Channels.Where(c => c.Enabled && c.Channel != NotificationChannel.Desktop).ToList();
        if (enabled.Count == 0) return;
        DateTimeOffset? pausedUntil;
        lock (gate) pausedUntil = deliveryPausedUntilUtc;
        if (NotificationDeliveryPolicy.IsPaused(pausedUntil, DateTimeOffset.UtcNow))
        {
            // Kept on the Notifications page by the caller; only the outside send is skipped, and the Activity log says so.
            activity.Record("Information", "Notifications", "Not sent outside (delivery paused)",
                $"{title} -> {string.Join(", ", enabled.Select(c => c.Channel))}");
            return;
        }
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
                        await DispatchDiscordAsync(channel, severity, title, message);
                        break;
                    case NotificationChannel.Email:
                        await DispatchEmailAsync(channel, severity, title, message);
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

    // v0.6.17.0: a Discord webhook is a plain HTTPS POST endpoint (Channel Settings > Integrations
    // > Webhooks) -- no bot, no token, no gateway connection needed for outbound-only notifications.
    // That's the real HeadlessDiscordBotService (a whole separate, opt-in gateway connection) for
    // two-way command handling; this is just the outbound leg, shaped like Discord's own embed API.
    private async Task DispatchDiscordAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.TargetUrl)) return;
        var color = severity switch
        {
            "Critical" or "Error" => 0xE74C3C,
            "Warning" => 0xF39C12,
            _ => 0x3498DB
        };
        var payload = new
        {
            embeds = new[]
            {
                new { title, description = message, color, timestamp = DateTimeOffset.UtcNow }
            }
        };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await HttpClient.PostAsJsonAsync(channel.TargetUrl, payload);
                if (response.IsSuccessStatusCode) return;
            }
            catch when (attempt == 0) { /* retry once */ }
        }
        activity.Record("Warning", "Notifications", "Discord dispatch failed after retry", channel.TargetUrl);
    }

    // v0.7.70.0: plain SMTP submission via the BCL's own SmtpClient rather than hand-rolling the
    // protocol (unlike PalworldRconService, which had no choice -- there is no BCL Source RCON
    // client). SmtpClient is officially "not recommended for new development" per Microsoft's own
    // guidance (MailKit et al. are suggested instead), but it is not obsolete/removed, ships in
    // .NET 10, and correctly handles STARTTLS/auth -- safer to reuse a battle-tested implementation
    // here than to hand-roll TLS negotiation for a feature this session has no live mail server to
    // validate against. Known limitation: SmtpClient only supports STARTTLS-style submission
    // (typically port 587), not implicit-TLS-from-connect (port 465) -- disclosed, not silently
    // unsupported.
    private async Task DispatchEmailAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.SmtpHost) || string.IsNullOrWhiteSpace(channel.EmailFrom) || string.IsNullOrWhiteSpace(channel.EmailTo))
        {
            activity.Record("Warning", "Notifications", "Email dispatch skipped", "SmtpHost, EmailFrom and EmailTo must all be configured.");
            return;
        }

        using var client = new SmtpClient(channel.SmtpHost, channel.SmtpPort) { EnableSsl = channel.SmtpUseSsl, Timeout = 10_000 };
        if (!string.IsNullOrWhiteSpace(channel.SmtpUsername))
            client.Credentials = new NetworkCredential(channel.SmtpUsername, channel.SmtpPassword ?? string.Empty);

        using var mail = new MailMessage(channel.EmailFrom, channel.EmailTo)
        {
            Subject = $"[MystTiq {severity}] {title}",
            Body = message
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try { await client.SendMailAsync(mail); return; }
            catch when (attempt == 0) { /* retry once */ }
        }
        activity.Record("Warning", "Notifications", "Email dispatch failed after retry", channel.SmtpHost);
    }

    private NotificationChannelConfiguration LoadChannels()
    {
        try { return File.Exists(channelsPath) ? JsonSerializer.Deserialize<NotificationChannelConfiguration>(File.ReadAllText(channelsPath)) ?? new() : new(); }
        catch { return new(); }
    }

    private DateTimeOffset? LoadDeliveryPause()
    {
        try { return File.Exists(deliveryPath) ? JsonSerializer.Deserialize<NotificationDeliveryPause>(File.ReadAllText(deliveryPath))?.PausedUntilUtc : null; }
        catch { return null; }
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
