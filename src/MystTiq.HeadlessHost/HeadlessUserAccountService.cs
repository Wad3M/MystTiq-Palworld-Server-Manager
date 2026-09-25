using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Security;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.114.0 "Multi-User Login": named human accounts (username + password) on top of the existing token
// model. Before this, every caller was a token: the shared legacy bearer token (Owner) or an RBAC principal
// token (HeadlessRbacService). Those still work unchanged. An account signs in with a password and gets a
// session token, which the same auth middleware accepts, so every existing RequireRole check, per-server
// scope and the Desktop's role-filtered UI apply to named people with no second permission system.
//
//   - Passwords: PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte random salt, stored as
//     "pbkdf2-sha256$<iterations>$<salt b64>$<hash b64>". Never stored or logged in plain text.
//   - Sessions: 32 random bytes, returned once; only the SHA-256 is persisted (sessions.json), so a leaked
//     file cannot be replayed. 12 hours from sign-in.
//   - The account is looked up on every request, so disabling it, changing its role or scope, or resetting
//     its password takes effect at once (disable and password reset also end its sessions).
//   - Sign-in failures: 5 in a row lock that account for 15 minutes, on top of the existing per-IP guard. A
//     missing username costs the same PBKDF2 work as a wrong password, so the timing does not reveal which.
public sealed record UserAccountRecord(
    string Id, string Username, string DisplayName, MystTiqRole Role, string? ScopedServerProfileId,
    string PasswordHash, bool Enabled, DateTimeOffset CreatedUtc, DateTimeOffset? LastLoginUtc);

public sealed record UserSessionRecord(string TokenSha256, string UserId, DateTimeOffset CreatedUtc, DateTimeOffset ExpiresUtc);

public sealed record UserAccountDto(
    string Id, string Username, string DisplayName, MystTiqRole Role, string? ScopedServerProfileId,
    bool Enabled, DateTimeOffset CreatedUtc, DateTimeOffset? LastLoginUtc, bool LockedOut);

public sealed record UserAccountCreateRequest(string? Username, string? DisplayName, MystTiqRole Role, string? Password, string? ScopedServerProfileId);

public sealed record UserAccountUpdateRequest(string? DisplayName, MystTiqRole Role, string? ScopedServerProfileId, bool Enabled);

public sealed record UserPasswordRequest(string? Password);

public sealed record UserOwnPasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserLoginRequest(string? Username, string? Password);

public sealed record UserLoginResult(bool Success, string Message, string? Token, DateTimeOffset? ExpiresUtc, MystTiqPrincipalDto? Principal);

public sealed record UserAccountResult(bool Success, string Message, UserAccountDto? Account);

public sealed partial class HeadlessUserAccountService
{
    public const int MinimumPasswordLength = 10;
    public const int MaximumFailedLogins = 5;
    public const int Pbkdf2Iterations = 210_000;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaximumAccounts = 200;
    private const string PrincipalIdPrefix = "user:";

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex UsernamePattern();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    // Verified against when the username does not exist, so a missing account costs the same time.
    private static readonly string DummyHash = HashPassword("not-a-real-password-" + Guid.NewGuid());

    private readonly object gate = new();
    private readonly string usersPath;
    private readonly string sessionsPath;
    private readonly HeadlessActivityLogService activity;
    private readonly Func<DateTimeOffset> clock;
    private readonly Dictionary<string, UserAccountRecord> users;
    private readonly Dictionary<string, UserSessionRecord> sessions;
    private readonly Dictionary<string, (int Failures, DateTimeOffset? LockedUntil)> failures = new(StringComparer.OrdinalIgnoreCase);

    public HeadlessUserAccountService(IServerPathProfile paths, HeadlessActivityLogService activity, Func<DateTimeOffset>? clock = null)
    {
        this.activity = activity;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        var root = Path.Combine(paths.ManagerRuntimeRoot, "security");
        Directory.CreateDirectory(root);
        usersPath = Path.Combine(root, "users.json");
        sessionsPath = Path.Combine(root, "sessions.json");
        users = Load<UserAccountRecord>(usersPath).ToDictionary(u => u.Id, StringComparer.Ordinal);
        var now = this.clock();
        sessions = Load<UserSessionRecord>(sessionsPath).Where(s => s.ExpiresUtc > now).ToDictionary(s => s.TokenSha256, StringComparer.Ordinal);
    }

    // ---- Password hashing (public for the harness) --------------------------------------------------------

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = (stored ?? string.Empty).Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations) || iterations < 10_000) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password ?? string.Empty), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    public static string? ValidatePassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength
            ? $"The password must be at least {MinimumPasswordLength} characters."
            : password.Length > 256 ? "The password is too long (256 characters at most)." : null;

    // ---- Account management (Owner) ----------------------------------------------------------------------

    public IReadOnlyList<UserAccountDto> List()
    {
        lock (gate) return users.Values.OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase).Select(ToDto).ToArray();
    }

    public UserAccountResult Create(UserAccountCreateRequest request, string actor)
    {
        var username = (request.Username ?? string.Empty).Trim();
        if (!UsernamePattern().IsMatch(username)) return new(false, "The username must be 3 to 32 letters, digits, dots, dashes or underscores.", null);
        if (ValidatePassword(request.Password) is { } passwordError) return new(false, passwordError, null);
        var record = new UserAccountRecord(Guid.NewGuid().ToString("N"), username,
            string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(), request.Role,
            string.IsNullOrWhiteSpace(request.ScopedServerProfileId) ? null : request.ScopedServerProfileId.Trim(),
            HashPassword(request.Password!), true, clock(), null);
        UserAccountDto dto;
        lock (gate)
        {
            if (users.Count >= MaximumAccounts) return new(false, $"At most {MaximumAccounts} accounts are allowed.", null);
            if (users.Values.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) return new(false, $"An account called '{username}' already exists.", null);
            users[record.Id] = record;
            PersistUsers();
            dto = ToDto(record);
        }
        activity.Record("Information", "Auth", "User account created", $"{username} ({record.Role}{(record.ScopedServerProfileId is null ? string.Empty : ", server " + record.ScopedServerProfileId)})", actor);
        return new(true, $"Created '{username}'.", dto);
    }

    public UserAccountResult Update(string id, UserAccountUpdateRequest request, string actor)
    {
        UserAccountRecord updated;
        UserAccountDto dto;
        lock (gate)
        {
            if (!users.TryGetValue(id, out var existing)) return new(false, "That account does not exist.", null);
            updated = existing with
            {
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? existing.Username : request.DisplayName.Trim(),
                Role = request.Role,
                ScopedServerProfileId = string.IsNullOrWhiteSpace(request.ScopedServerProfileId) ? null : request.ScopedServerProfileId.Trim(),
                Enabled = request.Enabled
            };
            if (existing.Role == MystTiqRole.Owner && (updated.Role != MystTiqRole.Owner || !updated.Enabled) && !OtherEnabledOwnerExists(id))
            {
                // Not fatal: the shared legacy token stays Owner. But say so rather than lock everyone out quietly.
                activity.Record("Warning", "Auth", "Last Owner account demoted or disabled", $"{existing.Username}; the shared API token is still Owner.", actor);
            }
            users[id] = updated;
            if (!updated.Enabled) EndSessions(id);
            PersistUsers();
            dto = ToDto(updated);
        }
        activity.Record("Information", "Auth", "User account updated", $"{updated.Username}: {updated.Role}, {(updated.Enabled ? "enabled" : "disabled")}", actor);
        return new(true, $"Updated '{updated.Username}'.", dto);
    }

    public UserAccountResult SetPassword(string id, string? password, string actor)
    {
        if (ValidatePassword(password) is { } passwordError) return new(false, passwordError, null);
        UserAccountRecord updated;
        UserAccountDto dto;
        lock (gate)
        {
            if (!users.TryGetValue(id, out var existing)) return new(false, "That account does not exist.", null);
            updated = existing with { PasswordHash = HashPassword(password!) };
            users[id] = updated;
            failures.Remove(updated.Username);
            EndSessions(id);
            PersistUsers();
            dto = ToDto(updated);
        }
        activity.Record("Information", "Auth", "User password reset", $"{updated.Username}; their open sessions were signed out.", actor);
        return new(true, $"Password reset for '{updated.Username}'. They have been signed out everywhere.", dto);
    }

    public UserAccountResult Delete(string id, string actor)
    {
        UserAccountRecord? removed;
        lock (gate)
        {
            if (!users.Remove(id, out removed)) return new(false, "That account does not exist.", null);
            EndSessions(id);
            PersistUsers();
        }
        activity.Record("Information", "Auth", "User account deleted", removed.Username, actor);
        return new(true, $"Deleted '{removed.Username}'.", null);
    }

    // ---- Sign-in -------------------------------------------------------------------------------------------

    public UserLoginResult Login(string? username, string? password)
    {
        var name = (username ?? string.Empty).Trim();
        var now = clock();
        UserAccountRecord? account;
        lock (gate)
        {
            if (failures.TryGetValue(name, out var f) && f.LockedUntil is { } until && until > now)
                return new(false, $"Too many failed sign-ins for this account. Try again after {until.ToLocalTime():HH:mm}.", null, null, null);
            account = users.Values.FirstOrDefault(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        var valid = VerifyPassword(password ?? string.Empty, account?.PasswordHash ?? DummyHash) && account is not null;
        if (!valid || account is null || !account.Enabled)
        {
            lock (gate)
            {
                var count = (failures.TryGetValue(name, out var f) ? f.Failures : 0) + 1;
                DateTimeOffset? lockedUntil = count >= MaximumFailedLogins ? now + LockoutDuration : null;
                failures[name] = (count, lockedUntil);
                if (lockedUntil is not null) activity.Record("Warning", "Auth", "User account locked after failed sign-ins", $"{name}; locked until {lockedUntil:O}");
            }
            // One message for every failure, so it does not reveal whether the username exists.
            return new(false, "The username or password is not right, or the account is disabled.", null, null, null);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var session = new UserSessionRecord(Hash(token), account.Id, now, now + SessionLifetime);
        lock (gate)
        {
            failures.Remove(name);
            foreach (var stale in sessions.Values.Where(s => s.ExpiresUtc <= now).Select(s => s.TokenSha256).ToArray()) sessions.Remove(stale);
            sessions[session.TokenSha256] = session;
            users[account.Id] = account with { LastLoginUtc = now };
            PersistSessions();
            PersistUsers();
        }
        activity.Record("Information", "Auth", "User signed in", account.Username, account.Username);
        var principal = ToPrincipal(account, session.ExpiresUtc);
        return new(true, $"Signed in as {account.DisplayName}.", token, session.ExpiresUtc,
            new MystTiqPrincipalDto(principal.Id, principal.Name, principal.Role, principal.ExpiresUtc, principal.IsExpired, principal.ScopedServerProfileId));
    }

    // Returns null for anything that is not a live session of an enabled account.
    public MystTiqPrincipal? Authenticate(string suppliedToken)
    {
        var hash = Hash(suppliedToken ?? string.Empty);
        var now = clock();
        lock (gate)
        {
            if (!sessions.TryGetValue(hash, out var session)) return null;
            if (session.ExpiresUtc <= now) { sessions.Remove(hash); PersistSessions(); return null; }
            if (!users.TryGetValue(session.UserId, out var account) || !account.Enabled) return null;
            return ToPrincipal(account, session.ExpiresUtc);
        }
    }

    public bool Logout(string suppliedToken)
    {
        lock (gate)
        {
            if (!sessions.Remove(Hash(suppliedToken ?? string.Empty), out var session)) return false;
            PersistSessions();
            if (users.TryGetValue(session.UserId, out var account)) activity.Record("Information", "Auth", "User signed out", account.Username, account.Username);
            return true;
        }
    }

    public UserAccountResult ChangeOwnPassword(MystTiqPrincipal principal, string? currentPassword, string? newPassword)
    {
        if (!principal.Id.StartsWith(PrincipalIdPrefix, StringComparison.Ordinal))
            return new(false, "Only a signed-in user account can change its own password.", null);
        var id = principal.Id[PrincipalIdPrefix.Length..];
        UserAccountRecord? account;
        lock (gate) users.TryGetValue(id, out account);
        if (account is null) return new(false, "That account no longer exists.", null);
        if (!VerifyPassword(currentPassword ?? string.Empty, account.PasswordHash)) return new(false, "The current password is not right.", null);
        return SetPassword(id, newPassword, account.Username);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    private static MystTiqPrincipal ToPrincipal(UserAccountRecord account, DateTimeOffset sessionExpiresUtc) => new()
    {
        Id = PrincipalIdPrefix + account.Id,
        Name = account.DisplayName,
        Role = account.Role,
        // The session's own end, so the Desktop can say when it has to sign in again.
        ExpiresUtc = sessionExpiresUtc,
        ScopedServerProfileId = account.ScopedServerProfileId
    };

    private UserAccountDto ToDto(UserAccountRecord u)
    {
        var locked = failures.TryGetValue(u.Username, out var f) && f.LockedUntil is { } until && until > clock();
        return new(u.Id, u.Username, u.DisplayName, u.Role, u.ScopedServerProfileId, u.Enabled, u.CreatedUtc, u.LastLoginUtc, locked);
    }

    private bool OtherEnabledOwnerExists(string exceptId) =>
        users.Values.Any(u => u.Id != exceptId && u.Enabled && u.Role == MystTiqRole.Owner);

    // Caller holds gate.
    private void EndSessions(string userId)
    {
        var ended = sessions.Values.Where(s => s.UserId == userId).Select(s => s.TokenSha256).ToArray();
        foreach (var key in ended) sessions.Remove(key);
        if (ended.Length > 0) PersistSessions();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static List<T> Load<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? [] : []; }
        catch { return []; }
    }

    private void PersistUsers() => Persist(usersPath, users.Values.ToList());
    private void PersistSessions() => Persist(sessionsPath, sessions.Values.ToList());

    private static void Persist<T>(string path, T value)
    {
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(partial, path, true);
    }
}
