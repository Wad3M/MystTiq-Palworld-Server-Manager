using System.Collections.Concurrent;

namespace PalworldManager.Services.Infrastructure;

public sealed class ScanCache
{
    private sealed record CacheEntry(object Value, DateTime ExpiresUtc);

    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    public void Set<T>(string key, T value, TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var ttl = lifetime ?? TimeSpan.FromMinutes(2);
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
        cache[key] = new CacheEntry(value!, DateTime.UtcNow.Add(ttl));
    }

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key) || !cache.TryGetValue(key, out var entry)) return false;
        if (entry.ExpiresUtc <= DateTime.UtcNow)
        {
            cache.TryRemove(key, out _);
            return false;
        }

        if (entry.Value is not T typed) return false;
        value = typed;
        return true;
    }

    public bool Invalidate(string key) =>
        !string.IsNullOrWhiteSpace(key) && cache.TryRemove(key, out _);

    public int RemoveExpired()
    {
        var removed = 0;
        var now = DateTime.UtcNow;
        foreach (var pair in cache)
        {
            if (pair.Value.ExpiresUtc > now) continue;
            if (cache.TryRemove(pair.Key, out _)) removed++;
        }
        return removed;
    }

    public void Clear() => cache.Clear();
}
