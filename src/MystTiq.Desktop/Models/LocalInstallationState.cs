// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.Models;

public enum LocalServiceState { Running, Stopped, NotInstalled, Unreachable, Unknown }
public enum LocalPalServerState { Found, NotFound, Unknown }
public enum LocalApiState { Connected, Unavailable, AuthenticationRequired, ConfigurationError, Unknown }

public sealed record LocalInstallationState(
    LocalServiceState Service,
    LocalPalServerState PalServer,
    LocalApiState Api,
    string? Detail = null);
