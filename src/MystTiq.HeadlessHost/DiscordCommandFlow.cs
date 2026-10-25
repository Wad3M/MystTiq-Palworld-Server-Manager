// MystTiq v1.1.5.0: file reviewed for this release (2026-10-25).
using MystTiq.Core.Security;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.9.1 (roadmap B-4): the reply side of one slash command, so the flow below runs against the real interaction or
/// a harness fake. Before <see cref="DeferAsync"/> only <see cref="RespondAsync"/> may be used; after it, only
/// <see cref="EditAsync"/> and <see cref="FollowupAsync"/>.
/// </summary>
public interface IDiscordCommandReply
{
    /// <summary>Acknowledges the command now ("MystTiq is thinking…"); Discord allows 3 seconds for this.</summary>
    Task DeferAsync();
    /// <summary>Answers the command at once (only before <see cref="DeferAsync"/>).</summary>
    Task RespondAsync(string text, bool ephemeral);
    /// <summary>Replaces the acknowledged reply with the result (within the interaction's 15 minutes).</summary>
    Task EditAsync(string text);
    /// <summary>Adds a further message, here only to the caller when <paramref name="ephemeral"/>.</summary>
    Task FollowupAsync(string text, bool ephemeral);
}

/// <summary>
/// v1.0.9.1 (roadmap B-4): what happens to one slash command, apart from the Discord gateway so the logic harness can
/// run it. Discord needs an answer within 3 seconds, and a start, stop, restart or backup takes far longer: before
/// v1.0.9.1 the bot answered only when the work was done, so Discord showed "The application did not respond" and the
/// result was lost even when the command worked. Now an authorized command is acknowledged before any work starts and the
/// reply is edited with the result. A refusal is answered at once and only to the caller.
/// </summary>
public static class DiscordCommandFlow
{
    public const string NotAuthorized = "You are not authorized to run this command.";
    public const string FailedPublicly = "Command failed. The reason was sent to you, and it is in MystTiq's activity log.";

    /// <param name="record">The activity log: level, title, detail.</param>
    /// <returns>What happened: "denied", "done" or "failed".</returns>
    public static async Task<string> RunAsync(IDiscordCommandReply reply, string name, MystTiqRole? granted,
        Func<Task<string>> execute, Action<string, string, string> record)
    {
        if (granted is null || granted < DiscordBotFormatting.RequiredRole(name))
        {
            record("Warning", $"Command denied: /{name}", "Caller's Discord roles do not map to a sufficient MystTiq role.");
            await reply.RespondAsync(NotAuthorized, ephemeral: true);
            return "denied";
        }

        await reply.DeferAsync();
        string text;
        try { text = await execute(); }
        catch (Exception ex)
        {
            record("Warning", $"Command failed: /{name}", ex.Message);
            await SendAsync(() => reply.EditAsync(FailedPublicly), name, record);
            await SendAsync(() => reply.FollowupAsync($"Command failed: {ex.Message}", ephemeral: true), name, record);
            return "failed";
        }
        record("Information", $"Command: /{name}", text);
        await SendAsync(() => reply.EditAsync(text), name, record);
        return "done";
    }

    // The work is done and recorded whether or not Discord takes the reply (an interaction expires after 15 minutes).
    private static async Task SendAsync(Func<Task> send, string name, Action<string, string, string> record)
    {
        try { await send(); }
        catch (Exception ex) { record("Warning", $"Discord reply not delivered: /{name}", ex.Message); }
    }
}
