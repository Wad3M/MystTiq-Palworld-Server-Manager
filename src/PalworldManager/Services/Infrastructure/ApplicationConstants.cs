namespace PalworldManager.Services.Infrastructure;

/// <summary>
/// Central non-user-configurable application constants. User settings remain in AppSettings.
/// Keeping operational defaults here prevents timing and filename drift across features.
/// </summary>
public static class ApplicationConstants
{
    public static class Timing
    {
        public static readonly TimeSpan UiHeartbeatInterval = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan AutomationInterval = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan IdleWatchdogInterval = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan ShutdownLogTailTimeout = TimeSpan.FromSeconds(1);
    }

    public static class Network
    {
        public static readonly TimeSpan StandardRequestTimeout = TimeSpan.FromSeconds(15);
    }
}
