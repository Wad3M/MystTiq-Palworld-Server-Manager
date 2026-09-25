namespace MystTiq.Desktop.Services;

// v0.8.14.0: what the signed-in role may do, in one place. Pure, so the logic harness can compile this file directly.
//
// The server enforces every route (RequireRole: Viewer < Operator < Admin < Owner); the Desktop mirrors it so a card
// the role cannot use at all is hidden rather than shown and refused. A card the role can read but not change stays
// visible and disabled. No principal means the local, token-less connection, which has full access, as before.
public static class RoleAccess
{
    public const int Viewer = 0;
    public const int Operator = 1;
    public const int Admin = 2;
    public const int Owner = 3;

    // An unknown role name gets the least access, never more.
    public static int Rank(string? role) => role switch
    {
        "Owner" => Owner,
        "Admin" => Admin,
        "Operator" => Operator,
        _ => Viewer
    };

    public static bool Allows(bool signedIn, string? role, int minimum) => !signedIn || Rank(role) >= minimum;

    // The line shown on pages with role-gated cards; empty when nothing is hidden.
    public static string HiddenNotice(bool signedIn, string? role) =>
        !signedIn || Rank(role) >= Owner
            ? string.Empty
            : $"Signed in as {(string.IsNullOrWhiteSpace(role) ? "an unknown role" : role)}: cards your role cannot use are hidden, and ones it can only read are shown read-only.";
}
