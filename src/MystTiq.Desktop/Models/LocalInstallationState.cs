// MystTiq v0.9.0.0: file reviewed for this release (2026-09-28).
namespace MystTiq.Desktop.Models;

public enum LocalServiceState { Running, Stopped, NotInstalled, Unreachable, Unknown }
public enum LocalPalServerState { Found, NotFound, Unknown }
public enum LocalApiState { Connected, Unavailable, AuthenticationRequired, ConfigurationError, Unknown }

public sealed record LocalInstallationState(
    LocalServiceState Service,
    LocalPalServerState PalServer,
    LocalApiState Api,
    string? Detail = null);
