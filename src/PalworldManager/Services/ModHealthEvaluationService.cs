using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Single source of truth for MOD health. UI surfaces must consume this result
/// instead of independently deriving Healthy/Active/Failed states.
/// </summary>
public sealed class ModHealthEvaluationService
{
    public ModHealthEvaluation Evaluate(
        ModRow mod,
        bool serverRunning,
        bool runtimeChecked,
        bool runtimeEvidenceFound = false,
        bool runtimeErrorFound = false,
        bool duplicateDetected = false)
    {
        ArgumentNullException.ThrowIfNull(mod);

        var isUe4ss = IsUe4ss(mod);
        var stateMismatch = mod.EnableReason.Contains("STATE MISMATCH", StringComparison.OrdinalIgnoreCase);

        if (!mod.Deployed)
            return Result(ModHealthStatus.Missing, 0, "Missing", "Required MOD files are missing.");

        if (isUe4ss && !mod.PresentInActiveRuntime)
            return Result(ModHealthStatus.Misconfigured, 20, "Misconfigured",
                "The MOD is not present beneath the resolver-selected Active UE4SS Mods Root.");

        if (runtimeErrorFound)
            return Result(ModHealthStatus.Failed, 15, "Failed", "Runtime error evidence was detected.");

        if (!mod.Enabled)
            return Result(ModHealthStatus.Disabled, 40, "Disabled", "The MOD is installed but disabled.");

        if (duplicateDetected || stateMismatch)
            return Result(ModHealthStatus.Attention, 55, "Attention",
                duplicateDetected
                    ? "Duplicate logical installation detected."
                    : "Configured and effective UE4SS activation state do not match.");

        if (!runtimeChecked)
            return Result(ModHealthStatus.Active, 70, serverRunning ? "Active" : "Installed",
                serverRunning
                    ? "Installed, enabled, and present. Runtime verification has not been run."
                    : "Installed and enabled. Start the server and verify runtime state.");

        // UE4SS/Lua requires positive runtime evidence while the server is running.
        if (isUe4ss)
        {
            if (serverRunning && (runtimeEvidenceFound || mod.LoadedByUe4ss))
                return Result(ModHealthStatus.Healthy, 100, "Healthy",
                    "UE4SS runtime load evidence confirms the MOD started.");

            return Result(ModHealthStatus.RuntimeUnverified, 70, "Runtime Unverified",
                serverRunning
                    ? "Installed, enabled, and present in the Active Mods Root, but no matching UE4SS load evidence was found."
                    : "Runtime load state cannot be confirmed while the server is offline.");
        }

        // PAK/Workshop packages do not require a UE4SS 'Starting Lua mod' entry.
        return Result(ModHealthStatus.Healthy, 100, "Healthy",
            "Installation and enabled-state verification passed. UE4SS Lua load evidence is not required for this MOD type.");
    }

    public static bool IsHealthy(ModHealthStatus status) => status == ModHealthStatus.Healthy;

    public static bool NeedsAttention(ModHealthStatus status) =>
        status is ModHealthStatus.RuntimeUnverified
            or ModHealthStatus.Misconfigured
            or ModHealthStatus.Attention
            or ModHealthStatus.Failed
            or ModHealthStatus.Missing;

    public static string ToDisplayText(ModHealthStatus status) => status switch
    {
        ModHealthStatus.Healthy => "Healthy",
        ModHealthStatus.Active => "Active",
        ModHealthStatus.RuntimeUnverified => "Runtime Unverified",
        ModHealthStatus.Misconfigured => "Misconfigured",
        ModHealthStatus.Attention => "Attention",
        ModHealthStatus.Failed => "Failed",
        ModHealthStatus.Disabled => "Disabled",
        ModHealthStatus.Missing => "Missing",
        _ => "Unknown"
    };

    private static bool IsUe4ss(ModRow mod) =>
        mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
        mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);

    private static ModHealthEvaluation Result(ModHealthStatus status, int score, string display, string detail) =>
        new(status, score, display, detail);
}

public sealed record ModHealthEvaluation(
    ModHealthStatus Status,
    int Score,
    string DisplayStatus,
    string Detail);
