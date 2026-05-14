using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public sealed class MemoryCacheServiceRiderState : IDisposable
{
    private readonly Timer _expirationTimer;

    internal ConcurrentDictionary<string, MemoryCacheServiceRider.CacheEntry> Cache { get; } = new(StringComparer.Ordinal);

    public MemoryCacheServiceRiderState()
    {
        _expirationTimer = new Timer(RemoveExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void RemoveExpiredEntries(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        var expiredKeys = Cache
            .AsParallel()
            .Where(kvp => kvp.Value.IsExpired(now))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            Cache.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _expirationTimer.Dispose();
        Cache.Clear();
    }
}
