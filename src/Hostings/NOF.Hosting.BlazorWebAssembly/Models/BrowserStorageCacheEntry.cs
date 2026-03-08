using Microsoft.Extensions.Caching.Distributed;

namespace NOF.Hosting.BlazorWebAssembly;

internal sealed record BrowserStorageCacheEntry(byte[] Data, DateTimeOffset? AbsoluteExpiration, TimeSpan? SlidingExpiration, DateTimeOffset LastAccessed)
{
    public static BrowserStorageCacheEntry FromBytes(byte[] data, DistributedCacheEntryOptions options)
    {
        DateTimeOffset? absoluteExpiration = null;
        if (options.AbsoluteExpiration is not null)
        {
            absoluteExpiration = options.AbsoluteExpiration.Value;
        }
        else if (options.AbsoluteExpirationRelativeToNow is not null)
        {
            absoluteExpiration = DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
        }

        return new BrowserStorageCacheEntry(data, absoluteExpiration, options.SlidingExpiration, DateTimeOffset.UtcNow);
    }

    public bool IsExpired()
    {
        var now = DateTimeOffset.UtcNow;
        if (AbsoluteExpiration is not null && now >= AbsoluteExpiration.Value)
        {
            return true;
        }

        if (SlidingExpiration is not null && now - LastAccessed >= SlidingExpiration.Value)
        {
            return true;
        }

        return false;
    }

    public BrowserStorageCacheEntry WithUpdatedAccess()
        => this with { LastAccessed = DateTimeOffset.UtcNow };

    public BrowserStorageCacheEntry WithAbsoluteExpiration(DateTimeOffset expiration)
        => this with { AbsoluteExpiration = expiration };

    public TimeSpan? GetTimeToLive()
    {
        var now = DateTimeOffset.UtcNow;
        if (AbsoluteExpiration is not null)
        {
            var ttl = AbsoluteExpiration.Value - now;
            return ttl > TimeSpan.Zero ? ttl : null;
        }

        if (SlidingExpiration is not null)
        {
            var ttl = SlidingExpiration.Value - (now - LastAccessed);
            return ttl > TimeSpan.Zero ? ttl : null;
        }

        return null;
    }
}
