using System.Net;
using System.Text.Json;

namespace MystTiq.Desktop.Services;

// v0.8.15.0: found by the real remote sign-in test. The server refuses a route the signed-in role may not use with
// 403 {"error":"insufficient-role"} (or {"error":"server-scope-mismatch"}), and a missing or expired sign-in with
// 401 {"error":"..."}. ReadOperationAsync used to parse that body as the route's own result type, so a refused save
// looked like it had worked. A refusal is now recognised and thrown with its status code. The sign-in route's own
// 401 (a result object with a message, no "error") is not a refusal and still comes back as a normal result.
public static class AccessRefusal
{
    public static string? Describe(HttpStatusCode status, string body)
    {
        if (status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            return null;

        string? error = null, required = null;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{\"error\":\"\"}" : body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                error = string.Empty;
            else if (document.RootElement.TryGetProperty("error", out var value))
            {
                error = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
                if (document.RootElement.TryGetProperty("required", out var minimum) && minimum.ValueKind == JsonValueKind.String)
                    required = minimum.GetString();
            }
        }
        catch (JsonException)
        {
            error = string.Empty;
        }

        return error switch
        {
            null => null,
            "insufficient-role" => required is { Length: > 0 }
                ? $"MystTiq refused this: it needs the {required} role or higher."
                : "MystTiq refused this: your role cannot do this.",
            "server-scope-mismatch" => "MystTiq refused this: your account is limited to a different server.",
            "missing-bearer-token" or "invalid-bearer-token" => "MystTiq refused this: not signed in, or the sign-in has expired. Sign in again.",
            _ => status == HttpStatusCode.Forbidden
                ? "MystTiq refused this: your account cannot do this."
                : "MystTiq refused this: not signed in, or the sign-in has expired. Sign in again.",
        };
    }
}
