namespace MystTiq.Desktop.Models;

public enum LocalServiceState { Running, Stopped, NotInstalled, Unreachable, Unknown }
public enum LocalPalServerState { Found, NotFound, Unknown }
public enum LocalApiState { Connected, Unavailable, AuthenticationRequired, ConfigurationError, Unknown }

public sealed record LocalInstallationState(
    LocalServiceState Service,
    LocalPalServerState PalServer,
    LocalApiState Api,
    string? Detail = null);
