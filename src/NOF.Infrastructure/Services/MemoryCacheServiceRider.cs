using Microsoft.Extensions.Caching.Distributed;
using NOF.Contract;
using System.Collections.Concurrent;
using System.Text;

namespace NOF.Infrastructure;

public sealed class MemoryCacheServiceRider : ICacheServiceRider, IDisposable
{
    internal static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();
    private static readonly Timer _expirationTimer = new(RemoveExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

    private static void RemoveExpiredEntries(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        var expiredKeys = _cache
            .AsParallel()
            .Where(kvp => kvp.Value.IsExpired(now))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        var unusedLocks = _localLocks
            .Where(kvp => kvp.Value.CurrentCount == 1)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in unusedLocks)
        {
            if (_localLocks.TryRemove(key, out var semaphore))
            {
                semaphore.Dispose();
            }
        }
    }

    public byte[]? Get(string key)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(now))
        {
            if (entry.SlidingExpiration is not null)
            {
                var newEntry = entry.WithUpdatedAccess(now);
                _cache.TryUpdate(key, newEntry, entry);
            }
            return entry.Data.ToArray();
        }

        return null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var entry = new CacheEntry(value, options);
        _cache[key] = entry;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(now))
        {
            if (entry.SlidingExpiration is not null)
            {
                var newEntry = entry.WithUpdatedAccess(now);
                _cache.TryUpdate(key, newEntry, entry);
            }
        }
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var exists = _cache.TryGetValue(key, out var entry) && !entry.IsExpired(now);
        return ValueTask.FromResult(exists);
    }

    public ValueTask<IReadOnlyDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, byte[]?>();
        var now = DateTimeOffset.UtcNow;

        foreach (var key in keys)
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(now))
            {
                result[key] = entry.Data.ToArray();
            }
            else
            {
                result[key] = null;
            }
        }
        return ValueTask.FromResult<IReadOnlyDictionary<string, byte[]?>>(result);
    }

    public ValueTask SetManyAsync(IDictionary<string, byte[]> items, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            _cache[item.Key] = new CacheEntry(item.Value, options);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        long count = 0;
        foreach (var key in keys)
        {
            if (_cache.TryRemove(key, out _))
            {
                count++;
            }
        }
        return ValueTask.FromResult(count);
    }

    public ValueTask<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
    {
        long newValue;

        while (true)
        {
            if (_cache.TryGetValue(key, out var oldEntry))
            {
                if (oldEntry.Data.Length != sizeof(long))
                {
                    throw new InvalidOperationException($"Cannot increment key '{key}': stored value is not a valid long integer (expected {sizeof(long)} bytes, got {oldEntry.Data.Length} bytes).");
                }

                var currentValue = BitConverter.ToInt64(oldEntry.Data.Span);
                newValue = currentValue + delta;
                var newEntry = new CacheEntry(BitConverter.GetBytes(newValue), new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = oldEntry.AbsoluteExpiration,
                    SlidingExpiration = oldEntry.SlidingExpiration
                });

                if (_cache.TryUpdate(key, newEntry, oldEntry))
                {
                    break;
                }
            }
            else
            {
                newValue = delta;
                var newEntry = new CacheEntry(BitConverter.GetBytes(newValue), new DistributedCacheEntryOptions());
                if (_cache.TryAdd(key, newEntry))
                {
                    break;
                }
            }
        }

        return ValueTask.FromResult(newValue);
    }

    public ValueTask<long> DecrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
    {
        return IncrementAsync(key, -delta, cancellationToken);
    }

    public ValueTask<bool> SetIfNotExistsAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var entry = new CacheEntry(value, options);
        var added = _cache.TryAdd(key, entry);
        return ValueTask.FromResult(added);
    }

    public ValueTask<byte[]?> GetAndSetAsync(string key, byte[] newValue, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var newEntry = new CacheEntry(newValue, options);
        var now = DateTimeOffset.UtcNow;
        byte[]? oldValue = null;

        _cache.AddOrUpdate(
            key,
            _ => newEntry,
            (_, existingEntry) =>
            {
                if (!existingEntry.IsExpired(now))
                {
                    oldValue = existingEntry.Data.ToArray();
                }
                return newEntry;
            });

        return ValueTask.FromResult(oldValue);
    }

    public ValueTask<byte[]?> GetAndRemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryRemove(key, out var entry) && !entry.IsExpired(DateTimeOffset.UtcNow))
        {
            return ValueTask.FromResult<byte[]?>(entry.Data.ToArray());
        }

        return ValueTask.FromResult<byte[]?>(null);
    }

    public ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(now))
        {
            var ttl = entry.GetTimeToLive(now);
            return ValueTask.FromResult(ttl is not null ? Optional.Of(ttl.Value) : Optional.None);
        }
        return ValueTask.FromResult<Optional<TimeSpan>>(Optional.None);
    }

    public ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        while (_cache.TryGetValue(key, out var oldEntry))
        {
            if (oldEntry.SlidingExpiration is not null)
            {
                throw new InvalidOperationException("Cannot set absolute expiration on entry with sliding expiration. Remove and re-add the entry instead.");
            }

            var newEntry = oldEntry.WithNewExpiration(DateTimeOffset.UtcNow.Add(expiration));
            if (_cache.TryUpdate(key, newEntry, oldEntry))
            {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> TryAcquireLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var acquired = _cache.TryAdd(key, new CacheEntry(Encoding.UTF8.GetBytes(lockId), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        }));
        return ValueTask.FromResult(acquired);
    }

    public ValueTask<bool> RenewLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        while (_cache.TryGetValue(key, out var oldEntry))
        {
            var storedLockId = Encoding.UTF8.GetString(oldEntry.Data.Span);
            if (storedLockId != lockId)
            {
                return ValueTask.FromResult(false);
            }

            var newEntry = oldEntry.WithNewExpiration(DateTimeOffset.UtcNow.Add(expiration));
            if (_cache.TryUpdate(key, newEntry, oldEntry))
            {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> ReleaseLockAsync(string key, string lockId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            var storedLockId = Encoding.UTF8.GetString(entry.Data.Span);
            if (storedLockId == lockId)
            {
                return ValueTask.FromResult(_cache.TryRemove(key, out _));
            }
        }

        return ValueTask.FromResult(false);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    internal sealed class CacheEntry
    {
        public ReadOnlyMemory<byte> Data { get; }
        public DateTimeOffset? AbsoluteExpiration { get; }
        public TimeSpan? SlidingExpiration { get; }
        private readonly DateTimeOffset _lastAccessed;

        public CacheEntry(byte[] data, DistributedCacheEntryOptions options)
            : this(data, options, DateTimeOffset.UtcNow)
        {
        }

        private CacheEntry(byte[] data, DistributedCacheEntryOptions options, DateTimeOffset lastAccessed)
        {
            Data = data;
            _lastAccessed = lastAccessed;
            SlidingExpiration = options.SlidingExpiration;

            if (options.AbsoluteExpiration is not null)
            {
                AbsoluteExpiration = options.AbsoluteExpiration.Value;
            }
            else if (options.AbsoluteExpirationRelativeToNow is not null)
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
        }

        public bool IsExpired(DateTimeOffset now)
        {
            if (AbsoluteExpiration is not null && now >= AbsoluteExpiration.Value)
            {
                return true;
            }

            if (SlidingExpiration is not null && now - _lastAccessed >= SlidingExpiration.Value)
            {
                return true;
            }

            return false;
        }

        public CacheEntry WithUpdatedAccess(DateTimeOffset now)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = AbsoluteExpiration,
                SlidingExpiration = SlidingExpiration
            };
            return new CacheEntry(Data.ToArray(), options, now);
        }

        public CacheEntry WithNewExpiration(DateTimeOffset expiration)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiration,
                SlidingExpiration = SlidingExpiration
            };
            return new CacheEntry(Data.ToArray(), options, _lastAccessed);
        }

        public TimeSpan? GetTimeToLive(DateTimeOffset now)
        {
            if (AbsoluteExpiration is not null)
            {
                var ttl = AbsoluteExpiration.Value - now;
                return ttl > TimeSpan.Zero ? ttl : null;
            }

            if (SlidingExpiration is not null)
            {
                var ttl = SlidingExpiration.Value - (now - _lastAccessed);
                return ttl > TimeSpan.Zero ? ttl : null;
            }

            return null;
        }
    }

}
