using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Collections.Concurrent;

namespace NOF.UI;

public abstract class BrowserStorageCacheService : ICacheService
{
    private readonly IBrowserStorage _browserStorage;
    private readonly ICacheSerializer _serializer;
    private readonly CacheServiceOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();

    protected BrowserStorageCacheService(
        IBrowserStorage browserStorage,
        ICacheSerializer serializer,
        ICacheLockRetryStrategy lockRetryStrategy,
        IOptions<CacheServiceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(browserStorage);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(lockRetryStrategy);
        ArgumentNullException.ThrowIfNull(options);

        _browserStorage = browserStorage;
        _serializer = serializer;
        _options = options.Value;
    }

    public byte[]? Get(string key)
        => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var entry = await ReadEntryAsync(key);
        if (entry is null)
        {
            return null;
        }

        if (entry.IsExpired())
        {
            await RemoveAsync(key, token);
            return null;
        }

        if (entry.SlidingExpiration is not null)
        {
            entry = entry.WithUpdatedAccess();
            await WriteEntryAsync(key, entry);
        }

        return entry.Data;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var entry = BrowserStorageCacheEntry.FromBytes(value, options);
        await WriteEntryAsync(key, entry);
    }

    public void Refresh(string key)
        => RefreshAsync(key).GetAwaiter().GetResult();

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        var entry = await ReadEntryAsync(key);
        if (entry is null)
        {
            return;
        }

        if (entry.IsExpired())
        {
            await RemoveAsync(key, token);
            return;
        }

        if (entry.SlidingExpiration is not null)
        {
            await WriteEntryAsync(key, entry.WithUpdatedAccess());
        }
    }

    public void Remove(string key)
        => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await RemoveCoreAsync(ApplyKeyPrefix(key));
    }

    public async ValueTask<Optional<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var data = await GetAsync(key, cancellationToken);
        if (data is null || data.Length == 0)
        {
            return Optional.None;
        }

        var value = _serializer.Deserialize<T>(data);
        return value is not null ? Optional.Of(value) : Optional.None;
    }

    public async ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<T>(key, cancellationToken);
        if (result.HasValue)
        {
            return result.Value;
        }

        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            result = await GetAsync<T>(key, cancellationToken);
            if (result.HasValue)
            {
                return result.Value;
            }

            var value = await factory(cancellationToken);
            await SetAsync(key, value, options, cancellationToken);
            return value;
        }
        finally
        {
            localLock.Release();
        }
    }

    public async ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = _serializer.Serialize(value);
        await SetAsync(key, data.ToArray(), options ?? _options.DefaultEntryOptions, cancellationToken);
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => await GetAsync(key, cancellationToken) is not null;

    public async ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Optional<T>>();
        foreach (var key in keys)
        {
            var data = await GetAsync(key, cancellationToken);
            if (data is null || data.Length == 0)
            {
                result[key] = Optional.None;
                continue;
            }

            var value = _serializer.Deserialize<T>(data);
            result[key] = value is not null ? Optional.Of(value) : Optional.None;
        }

        return result;
    }

    public async ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await SetAsync(item.Key, _serializer.Serialize(item.Value).ToArray(), options ?? _options.DefaultEntryOptions, cancellationToken);
        }
    }

    public async ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        long count = 0;
        foreach (var key in keys)
        {
            if (await GetAsync(key, cancellationToken) is not null)
            {
                await RemoveAsync(key, cancellationToken);
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            var data = await GetAsync(key, cancellationToken);
            long newValue;
            if (data is null)
            {
                newValue = value;
                await SetAsync(key, BitConverter.GetBytes(newValue), _options.DefaultEntryOptions, cancellationToken);
                return newValue;
            }

            if (data.Length != sizeof(long))
            {
                throw new InvalidOperationException($"Cannot increment key '{key}': stored value is not a valid long integer (expected {sizeof(long)} bytes, got {data.Length} bytes).");
            }

            newValue = BitConverter.ToInt64(data) + value;
            await SetAsync(key, BitConverter.GetBytes(newValue), _options.DefaultEntryOptions, cancellationToken);
            return newValue;
        }
        finally
        {
            localLock.Release();
        }
    }

    public ValueTask<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
        => IncrementAsync(key, -value, cancellationToken);

    public async ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            if (await GetAsync(key, cancellationToken) is not null)
            {
                return false;
            }

            await SetAsync(key, value, options, cancellationToken);
            return true;
        }
        finally
        {
            localLock.Release();
        }
    }

    public async ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            var oldValue = await GetAsync<T>(key, cancellationToken);
            await SetAsync(key, newValue, options, cancellationToken);
            return oldValue;
        }
        finally
        {
            localLock.Release();
        }
    }

    public async ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            var oldValue = await GetAsync<T>(key, cancellationToken);
            await RemoveAsync(key, cancellationToken);
            return oldValue;
        }
        finally
        {
            localLock.Release();
        }
    }

    public async ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var entry = await ReadEntryAsync(key);
        if (entry is null || entry.IsExpired())
        {
            return Optional.None;
        }

        var ttl = entry.GetTimeToLive();
        return ttl is not null ? Optional.Of(ttl.Value) : Optional.None;
    }

    public async ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            var entry = await ReadEntryAsync(key);
            if (entry is null || entry.IsExpired())
            {
                await RemoveAsync(key, cancellationToken);
                return false;
            }

            if (entry.SlidingExpiration is not null)
            {
                throw new InvalidOperationException("Cannot set absolute expiration on entry with sliding expiration. Remove and re-add the entry instead.");
            }

            await WriteEntryAsync(key, entry.WithAbsoluteExpiration(DateTimeOffset.UtcNow.Add(expiration)));
            return true;
        }
        finally
        {
            localLock.Release();
        }
    }

    public ValueTask<IDistributedLock> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
        => ValueTask.FromException<IDistributedLock>(new NotSupportedException($"Distributed locks are not supported for {GetBrowserStorageName()} browser storage cache."));

    public ValueTask<Optional<IDistributedLock>> TryAcquireLockAsync(string key, TimeSpan expiration, TimeSpan timeout, CancellationToken cancellationToken = default)
        => ValueTask.FromException<Optional<IDistributedLock>>(new NotSupportedException($"Distributed locks are not supported for {GetBrowserStorageName()} browser storage cache."));

    private string ApplyKeyPrefix(string key)
        => string.IsNullOrEmpty(_options.KeyPrefix) ? key : _options.KeyPrefix + key;

    private async ValueTask<BrowserStorageCacheEntry?> ReadEntryAsync(string key)
    {
        var data = await GetStringCoreAsync(ApplyKeyPrefix(key));
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        return _serializer.Deserialize<BrowserStorageCacheEntry>(Convert.FromBase64String(data));
    }

    private async ValueTask WriteEntryAsync(string key, BrowserStorageCacheEntry entry)
    {
        await SetStringCoreAsync(ApplyKeyPrefix(key), Convert.ToBase64String(_serializer.Serialize(entry).ToArray()));
    }

    private ValueTask<string?> GetStringCoreAsync(string key)
        => _browserStorage.GetItemAsync(key);

    private ValueTask SetStringCoreAsync(string key, string value)
        => _browserStorage.SetItemAsync(key, value);

    private ValueTask RemoveCoreAsync(string key)
        => _browserStorage.RemoveItemAsync(key);

    private string GetBrowserStorageName()
        => _browserStorage switch
        {
            ILocalStorage => "local",
            ISessionStorage => "session",
            _ => "unknown"
        };
}

