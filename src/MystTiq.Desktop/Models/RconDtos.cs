// MystTiq v1.1.1.2: file reviewed for this release (2026-10-19).
namespace MystTiq.Desktop.Models;

public sealed record RconStatusDto(bool Enabled, int Port, bool PasswordConfigured, string Endpoint, string Detail);
public sealed record RconDoctorResultDto(bool Success, RconStatusDto Status, bool TcpReachable, bool Authenticated, string Detail, IReadOnlyList<string> Checks);
public sealed record RconCommandRequestDto(string Command);
public sealed record RconCommandResultDto(bool Success, string Command, string Response, string Message, DateTimeOffset ExecutedAt);
