// MystTiq v1.1.5.0: file reviewed for this release (2026-10-24).
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.Threading.Channels;
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
// construction below still compiling unchanged. v1.0.7.4 (roadmap Q-14): SmtpPassword and TargetUrl are
// encrypted in channels.json (DPAPI on Windows, an owner-only file on Linux); see SecretProtector.
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
public sealed record NotificationDeliveryState(DateTimeOffset? PausedUntilUtc, IReadOnlyList<string> ExternalChannels, string Detail)
{
    // v1.0.7.1 (roadmap Q-11): the outside send queue.
    public NotificationSendQueueState? Queue { get; init; }
}

// v1.0.7.1 (roadmap Q-11): what is waiting to be sent outside, and what the queue combined or dropped since MystTiq started.
public sealed record NotificationSendQueueState(int Waiting, DateTimeOffset? OldestWaitingSinceUtc, int Capacity, int Senders, long Combined, long Dropped, int RepeatWindowMinutes);
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
    // v1.0.2.0 (roadmap R-2): every outside send and its result.
    private readonly NotificationDeliveryLog deliveries;

    // v1.0.7.1 (roadmap Q-11): outside sends used to start one background task per notification, with no limit, so a fault
    // raising hundreds of alerts started hundreds of overlapping webhook, Discord and email sends. They now go through a
    // bounded queue read by a fixed number of senders. The same alert (same severity, title and text) within the repeat
    // window is combined: the first is sent, the repeats are counted, and when the window ends one message says how many
    // more there were. A send that finds the queue full is dropped and recorded for each channel; one that waited in the
    // queue says how long.
    public const int SendQueueCapacity = 200;
    public const int SenderCount = 2;
    public static TimeSpan RepeatWindow { get; set; } = TimeSpan.FromMinutes(10);
    public static TimeSpan RepeatCheckEvery { get; set; } = TimeSpan.FromSeconds(30);
    private readonly Channel<PendingSend> sendQueue = Channel.CreateBounded<PendingSend>(new BoundedChannelOptions(SendQueueCapacity) { FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentDictionary<long, DateTimeOffset> waitingSince = new();
    private readonly Dictionary<string, RepeatState> repeats = new(StringComparer.Ordinal);
    private readonly object repeatGate = new();
    private long nextSendId;
    private long combinedCount;
    private long droppedCount;
    private int sendersStarted;

    private sealed record PendingSend(long Id, List<NotificationChannelConfig> Channels, string Severity, string Title, string Message, DateTimeOffset QueuedUtc);
    private sealed class RepeatState(List<NotificationChannelConfig> channels, string severity, string title, string message, DateTimeOffset firstUtc)
    {
        public List<NotificationChannelConfig> Channels { get; } = channels;
        public string Severity { get; } = severity;
        public string Title { get; } = title;
        public string Message { get; } = message;
        public DateTimeOffset FirstUtc { get; } = firstUtc;
        public int Repeats { get; set; }
    }

    public HeadlessNotificationRoutingService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.activity = activity;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "notifications");
        Directory.CreateDirectory(root);
        channelsPath = Path.Combine(root, "channels.json");
        templatesPath = Path.Combine(root, "templates.json");
        deliveryPath = Path.Combine(root, "delivery.json");
        deliveries = new NotificationDeliveryLog(root);
        channels = LoadChannels();
        deliveryPausedUntilUtc = LoadDeliveryPause();
        templates = LoadTemplates();
    }

    public NotificationChannelConfiguration GetChannels() { lock (gate) return channels; }

    // v1.0.7.4 (roadmap Q-9): what the API returns. The SMTP password is never sent back (like the Discord bot token).
    public NotificationChannelConfiguration GetChannelsView()
    {
        lock (gate) return new NotificationChannelConfiguration { Channels = channels.Channels.Select(c => c with { SmtpPassword = null }).ToList() };
    }

    public NotificationChannelConfiguration SaveChannels(NotificationChannelConfiguration updated)
    {
        lock (gate)
        {
            // v1.0.7.4 (roadmap Q-9): a save without a password (the view never carries one) keeps the stored one; an empty
            // password clears it.
            updated = new NotificationChannelConfiguration
            {
                Channels = updated.Channels.Select(c => c.SmtpPassword is null
                    ? c with { SmtpPassword = channels.Channels.FirstOrDefault(old => old.Channel == c.Channel)?.SmtpPassword }
                    : c.SmtpPassword.Length == 0 ? c with { SmtpPassword = null } : c).ToList()
            };
            PersistChannels(updated);
            channels = updated;
            return channels;
        }
    }

    public IReadOnlyList<NotificationTemplate> GetTemplates() { lock (gate) return templates; }

    public IReadOnlyList<NotificationTemplate> SaveTemplates(List<NotificationTemplate> updated)
    {
        lock (gate)
        {
            Persist(templatesPath, updated);
            templates = updated;
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
        return new(until, external, detail) { Queue = GetQueueState() };
    }

    public NotificationSendQueueState GetQueueState()
    {
        var waiting = waitingSince.Values.ToArray();
        return new(waiting.Length, waiting.Length == 0 ? null : waiting.Min(), SendQueueCapacity, SenderCount,
            Interlocked.Read(ref combinedCount), Interlocked.Read(ref droppedCount), (int)Math.Max(1, RepeatWindow.TotalMinutes));
    }

    // v1.0.2.0 (roadmap R-2): each outside channel's delivery health over the last 7 days, and the latest sends.
    public NotificationDeliveryHealth GetDeliveryHealth()
    {
        List<(string, bool)> external;
        lock (gate) external = channels.Channels.Where(c => c.Channel != NotificationChannel.Desktop).Select(c => (c.Channel.ToString(), c.Enabled)).ToList();
        return NotificationDeliveryLog.Health(external, deliveries.Snapshot(), DateTimeOffset.UtcNow, NotificationDeliveryLog.Window);
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
    public void Dispatch(string severity, string title, string message, bool combineRepeats = true)
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
        var now = DateTimeOffset.UtcNow;
        var key = $"{severity}\n{title}\n{message}";
        lock (repeatGate)
        {
            FlushRepeats(now);
            if (combineRepeats)
            {
                if (repeats.TryGetValue(key, out var seen))
                {
                    seen.Repeats++;
                    Interlocked.Increment(ref combinedCount);
                    return;
                }
                repeats[key] = new RepeatState(enabled, severity, title, message, now);
            }
        }
        Enqueue(enabled, severity, title, message, now);
    }

    // Caller holds repeatGate. A window that has ended is closed; if the alert repeated, one message says how often.
    private void FlushRepeats(DateTimeOffset now)
    {
        foreach (var (key, state) in repeats.Where(r => now - r.Value.FirstUtc >= RepeatWindow).ToList())
        {
            repeats.Remove(key);
            if (state.Repeats == 0) continue;
            var times = state.Repeats == 1 ? "time" : "times";
            Enqueue(state.Channels, state.Severity, $"{state.Title} (repeated {state.Repeats} more {times})",
                $"{state.Message}\n\nThis alert repeated {state.Repeats} more {times} between {state.FirstUtc:HH:mm} and {now:HH:mm} UTC; the repeats were combined into this message.",
                now);
        }
    }

    private void Enqueue(List<NotificationChannelConfig> enabled, string severity, string title, string message, DateTimeOffset now)
    {
        var send = new PendingSend(Interlocked.Increment(ref nextSendId), enabled, severity, title, message, now);
        waitingSince[send.Id] = now;
        if (!sendQueue.Writer.TryWrite(send))
        {
            waitingSince.TryRemove(send.Id, out _);
            Interlocked.Increment(ref droppedCount);
            foreach (var channel in enabled)
                deliveries.Record(new NotificationDeliveryRecord(now, channel.Channel.ToString(), title, false,
                    $"Dropped: {SendQueueCapacity} sends were already waiting to go out.", 0));
            return;
        }
        if (Interlocked.CompareExchange(ref sendersStarted, 1, 0) == 0)
            for (var i = 1; i <= SenderCount; i++)
                _ = BackgroundWork.Shared.Run($"outside notification sender {i}", SendLoopAsync);
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        var reader = sendQueue.Reader;
        while (!token.IsCancellationRequested)
        {
            using var tick = CancellationTokenSource.CreateLinkedTokenSource(token);
            tick.CancelAfter(RepeatCheckEvery);
            try
            {
                if (!await reader.WaitToReadAsync(tick.Token)) return;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // Quiet for a while: close the repeat windows that have ended, so their summaries go out.
                lock (repeatGate) FlushRepeats(DateTimeOffset.UtcNow);
                continue;
            }
            while (reader.TryRead(out var send))
            {
                waitingSince.TryRemove(send.Id, out _);
                await DispatchAsync(send.Channels, send.Severity, send.Title, send.Message, DateTimeOffset.UtcNow - send.QueuedUtc);
            }
        }
    }

    private async Task DispatchAsync(List<NotificationChannelConfig> enabled, string severity, string title, string message, TimeSpan queued)
    {
        var waitedText = queued >= TimeSpan.FromSeconds(5) ? $" (waited {queued.TotalSeconds:0} s in the send queue)" : string.Empty;
        foreach (var channel in enabled)
        {
            (bool Success, string Detail, int Attempts) outcome;
            try
            {
                outcome = channel.Channel switch
                {
                    NotificationChannel.Webhook => await DispatchWebhookAsync(channel, severity, title, message),
                    NotificationChannel.Discord => await DispatchDiscordAsync(channel, severity, title, message),
                    NotificationChannel.Email => await DispatchEmailAsync(channel, severity, title, message),
                    _ => (false, "Unknown channel.", 0)
                };
            }
            catch (Exception ex)
            {
                activity.Record("Warning", "Notifications", $"{channel.Channel} dispatch failed", ex.Message);
                outcome = (false, ex.Message, 1);
            }
            // v1.0.2.0 (roadmap R-2): every send is recorded, delivered or not.
            deliveries.Record(new NotificationDeliveryRecord(DateTimeOffset.UtcNow, channel.Channel.ToString(), title, outcome.Success, outcome.Detail + waitedText, outcome.Attempts));
        }
    }

    // v1.0.2.0 (roadmap R-2): a POST to an HTTPS endpoint, retried once; the answer (or error) is the send's result.
    private static async Task<(bool Success, string Detail, int Attempts)> PostAsync(string url, object payload)
    {
        var detail = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var response = await HttpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode) return (true, $"HTTP {(int)response.StatusCode}", attempt);
                detail = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { detail = ex.Message; }
        }
        return (false, detail, 2);
    }

    private async Task<(bool Success, string Detail, int Attempts)> DispatchWebhookAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.TargetUrl)) return (false, "No webhook URL is set.", 0);
        var payload = new { severity, title, message, createdUtc = DateTimeOffset.UtcNow };
        var result = await PostAsync(channel.TargetUrl, payload);
        // v1.0.7.4 (roadmap Q-9): the URL is its own password; only its host is logged.
        if (!result.Success) activity.Record("Warning", "Notifications", "Webhook dispatch failed after retry", SecretRedaction.Url(channel.TargetUrl));
        return result;
    }

    // v0.6.17.0: a Discord webhook is a plain HTTPS POST endpoint (Channel Settings > Integrations
    // > Webhooks) -- no bot, no token, no gateway connection needed for outbound-only notifications.
    // That's the real HeadlessDiscordBotService (a whole separate, opt-in gateway connection) for
    // two-way command handling; this is just the outbound leg, shaped like Discord's own embed API.
    private async Task<(bool Success, string Detail, int Attempts)> DispatchDiscordAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.TargetUrl)) return (false, "No Discord webhook URL is set.", 0);
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
        var result = await PostAsync(channel.TargetUrl, payload);
        if (!result.Success) activity.Record("Warning", "Notifications", "Discord dispatch failed after retry", SecretRedaction.Url(channel.TargetUrl));
        return result;
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
    private async Task<(bool Success, string Detail, int Attempts)> DispatchEmailAsync(NotificationChannelConfig channel, string severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(channel.SmtpHost) || string.IsNullOrWhiteSpace(channel.EmailFrom) || string.IsNullOrWhiteSpace(channel.EmailTo))
        {
            activity.Record("Warning", "Notifications", "Email dispatch skipped", "SmtpHost, EmailFrom and EmailTo must all be configured.");
            return (false, "SmtpHost, EmailFrom and EmailTo must all be configured.", 0);
        }

        using var client = new SmtpClient(channel.SmtpHost, channel.SmtpPort) { EnableSsl = channel.SmtpUseSsl, Timeout = 10_000 };
        if (!string.IsNullOrWhiteSpace(channel.SmtpUsername))
            client.Credentials = new NetworkCredential(channel.SmtpUsername, channel.SmtpPassword ?? string.Empty);

        using var mail = new MailMessage(channel.EmailFrom, channel.EmailTo)
        {
            Subject = $"[MystTiq {severity}] {title}",
            Body = message
        };

        var detail = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try { await client.SendMailAsync(mail); return (true, $"Accepted by {channel.SmtpHost}", attempt); }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException) { detail = ex.Message; }
        }
        activity.Record("Warning", "Notifications", "Email dispatch failed after retry", channel.SmtpHost);
        return (false, detail, 2);
    }

    // v1.0.7.0 (Q-1): unreadable channels or templates are never overwritten; a delivery pause just ends.
    // v1.0.7.4 (roadmap Q-14): the SMTP password and the webhook URLs are encrypted on disk (Windows) or the file is made
    // owner-only (Linux). Plain ones from an older version are moved over at start-up, keeping one plain copy beside it.
    private NotificationChannelConfiguration LoadChannels()
    {
        var loaded = SavedFiles.Load(channelsPath, "notification channels", () => new NotificationChannelConfiguration());
        var plain = new NotificationChannelConfiguration
        {
            Channels = loaded.Channels.Select(c => c with { TargetUrl = SecretProtector.Unprotect(c.TargetUrl), SmtpPassword = SecretProtector.Unprotect(c.SmtpPassword) }).ToList()
        };
        if (loaded.Channels.Any(c => SecretProtector.NeedsProtection(c.TargetUrl) || SecretProtector.NeedsProtection(c.SmtpPassword)))
        {
            try
            {
                SecretProtector.KeepPlainCopyOnce(channelsPath);
                PersistChannels(plain);
                activity.Record("Information", "Notifications", "Secrets encrypted", "The SMTP password and webhook URLs in channels.json are now encrypted.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SavedFileRefusedException) { }
        }
        SecretProtector.RestrictToOwner(channelsPath);
        return plain;
    }

    private void PersistChannels(NotificationChannelConfiguration value)
    {
        Persist(channelsPath, new NotificationChannelConfiguration
        {
            Channels = value.Channels.Select(c => c with { TargetUrl = SecretProtector.Protect(c.TargetUrl), SmtpPassword = SecretProtector.Protect(c.SmtpPassword) }).ToList()
        });
        SecretProtector.RestrictToOwner(channelsPath);
    }

    private DateTimeOffset? LoadDeliveryPause() =>
        // An older pause coming back would silently stop outside alerts, so an unreadable one means not paused.
        SavedFiles.Load(deliveryPath, "notification delivery pause", () => new NotificationDeliveryPause(null), ownerData: false, useGoodCopy: false).PausedUntilUtc;

    private List<NotificationTemplate> LoadTemplates() =>
        SavedFiles.Load(templatesPath, "notification templates", () => new List<NotificationTemplate>());

    private static void Persist<T>(string path, T value) => SavedFiles.Save(path, value);
}
