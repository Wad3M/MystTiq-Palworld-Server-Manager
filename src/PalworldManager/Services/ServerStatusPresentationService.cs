namespace PalworldManager.Services;

/// <summary>
/// Converts server lifecycle state into consistent user-facing text and color.
/// This keeps dashboard, header, and sidebar status presentation synchronized.
/// </summary>
public sealed class ServerStatusPresentationService
{
    public ServerStatusPresentation Describe(ServerLifecycleState state)
    {
        var color = state switch
        {
            ServerLifecycleState.Running => Color.FromRgb(53, 211, 107),
            ServerLifecycleState.Starting or ServerLifecycleState.Stopping => Color.FromRgb(240, 178, 70),
            ServerLifecycleState.Hung or ServerLifecycleState.Crashed or ServerLifecycleState.NotInstalled => Color.FromRgb(240, 91, 87),
            _ => Color.FromRgb(240, 178, 70)
        };

        var sidebarText = state switch
        {
            ServerLifecycleState.NotInstalled => "Not Installed",
            ServerLifecycleState.Hung => "Hung",
            ServerLifecycleState.Crashed => "Crashed",
            _ => state.ToString()
        };

        var healthText = state switch
        {
            ServerLifecycleState.Running => "HEALTHY",
            ServerLifecycleState.Starting => "STARTING",
            ServerLifecycleState.Stopping => "STOPPING",
            ServerLifecycleState.Hung => "NEEDS ATTENTION",
            ServerLifecycleState.Crashed => "FAILED",
            ServerLifecycleState.NotInstalled => "NOT INSTALLED",
            _ => "STOPPED"
        };

        return new ServerStatusPresentation(
            state.ToString().ToUpperInvariant(),
            sidebarText,
            healthText,
            new SolidColorBrush(color));
    }
}

public sealed record ServerStatusPresentation(
    string HeaderText,
    string SidebarText,
    string HealthText,
    SolidColorBrush Brush);
