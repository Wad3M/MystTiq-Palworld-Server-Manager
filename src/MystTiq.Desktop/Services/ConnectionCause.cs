// MystTiq v1.1.1.1: file reviewed for this release (2026-10-18).
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.7.2 (roadmap Q-7): the Dashboard said "Connection failed" whatever went wrong, so a stopped service, a rejected
/// sign-in and a certificate mismatch looked the same, and a Palworld whose REST API had stopped answering only showed as
/// "Player discovery unavailable" on the Players page. This names the cause and the next step. No Avalonia dependency, so
/// the logic harness tests it directly.
/// </summary>
public static class ConnectionCause
{
    public const string Reconnecting = "Reconnecting";
    public const string Unreachable = "Service unreachable";
    public const string SignInRejected = "Sign-in rejected";
    public const string CertificateRejected = "Certificate rejected";
    public const string ServiceError = "Service error";
    public const string Failed = "Connection failed";

    /// <summary>The state and the cause for a failed call. wasConnected: this tab had a working connection before.</summary>
    public static (string State, string Cause) ForFailure(Exception error, bool wasConnected, int attempt, Uri? address)
    {
        var where = address is null ? "the service's address" : address.GetLeftPart(UriPartial.Authority);
        var inner = error;
        while (inner.InnerException is not null && inner is not AuthenticationException && inner is not SocketException) inner = inner.InnerException;
        if (error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
            return (SignInRejected, "The MystTiq service refused this connection's sign-in (its token or session). Sign in again, or check the token in the connection settings.");
        if (error is HttpRequestException { StatusCode: { } status } && (int)status >= 500)
            return (ServiceError, $"The MystTiq service answered with an error (HTTP {(int)status}). Its log says why.");
        if (inner is AuthenticationException)
            return (CertificateRejected, "The service's certificate does not match the one pinned for this connection. Check the certificate fingerprint in the connection settings.");
        var noAnswer = error is TaskCanceledException or TimeoutException || inner is SocketException ||
                       error is HttpRequestException { StatusCode: null };
        if (noAnswer)
            return wasConnected
                ? (Reconnecting, $"The MystTiq service at {where} stopped answering. Trying again every 5 s (attempt {attempt}).")
                : (Unreachable, $"Nothing answers at {where}. Start the MystTiq service, or check the address and port.");
        return (Failed, error.Message);
    }

    /// <summary>The cause to show while connected: Palworld running with its REST API not answering, else none.</summary>
    public static string ForConnected(bool serverReady, bool playersAvailable, string playersDetail) =>
        serverReady && !playersAvailable
            ? "Palworld is running, but its REST API is not answering, so players and live data are unavailable. " + playersDetail
            : string.Empty;
}
