namespace MystTiq.Desktop.Models;

/// <summary>
/// Persistable connection metadata. Bearer tokens are still deliberately NOT a field on this
/// record -- they stay process-memory-only for the lifetime of a TabSession. As of v0.7.71.0 they
/// CAN be remembered across app restarts, but via a separate side-store keyed by <see cref="Id"/>
/// (CredentialStore, Windows DPAPI-encrypted), not by widening this model. See CredentialStore's
/// own header comment and memory: project_mysttiq_credential_storage_plan.md for the design.
/// </summary>
public sealed record ConnectionProfile(
    string Id,
    string Name,
    Uri BaseAddress,
    string? ServerCertificateSha256 = null,
    // v0.7.52.0: per-server identity color (item 2) -- one of ThemeCatalog.TabIdentityColorNames,
    // assigned once when a profile is first created and persisted from then on, so it survives tab
    // reordering/reopening across sessions rather than being re-derived from live tab position.
    // Distinct from TabSession.StatusDotColorKey, which is a live health signal (green/amber/red), not
    // an identity marker -- the two are meant to coexist, not merge.
    string AccentColorKey = "Blue",
    // Null means "use this host's unprefixed legacy routes" (the historical, single-server
    // behavior -- the backend hard-wires those to its "default" profile for backward compatibility).
    // Set this to target a specific profile on a multi-server fleet host via its
    // /api/v1/servers/{ServerId}/... routes instead -- see MystTiqApiClient's routing handler.
    string? ServerId = null,
    // v0.7.79.0: requested directly -- accent theme/light-dark mode used to be one app-wide setting
    // (LocalThemePreferencesStore), so switching tabs never changed how the app looked, only which
    // server it talked to. Now each connection remembers its own appearance, same pattern as
    // AccentColorKey above: switching ActiveTab re-applies THIS profile's own AccentTheme/
    // ThemeVariant (see MainWindowViewModel.ActiveTab's setter and SelectedAccentTheme/IsLightMode,
    // which read/write through to whichever profile the active tab holds instead of a shared field).
    string AccentTheme = "Default",
    string ThemeVariant = "Dark")
{
    public static ConnectionProfile LocalDefault { get; } =
        new("local-default", "Local MystTiq", new Uri("http://127.0.0.1:8213"));

    public bool UsesHttps => BaseAddress.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
