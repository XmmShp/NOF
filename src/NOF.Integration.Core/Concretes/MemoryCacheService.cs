using Microsoft.Extensions.Caching.Distributed;
using NOF.Caching;
using System.Collections.Concurrent;

namespace NOF;

/// <summary>
/// In-memory implementation of <see cref="ICacheService"/> for development and testing purposes.
/// </summary>
public sealed class MemoryCacheService : ICacheServiceWithRawAccess, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ICacheSerializer _serializer;
    private readonly ILockRetryStrategy _lockRetryStrategy;
    private readonly Timer _expirationTimer;
    private readonly CacheServiceOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();

    public MemoryCacheService(ICacheSerializer serializer, ILockRetryStrategy lockRetryStrategy, CacheServiceOptions configurator)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(lockRetryStrategy);
        ArgumentNullException.ThrowIfNull(configurator);

        _serializer = serializer;
        _lockRetryStrategy = lockRetryStrategy;
        _options = configurator;
        _expirationTimer = new Timer(RemoveExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private string ApplyKeyPrefix(string key)
    {
        return string.IsNullOrEmpty(_options.KeyPrefix) ? key : _options.KeyPrefix + key;
    }

    private void RemoveExpiredEntries(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        // Parallelize expiration check for better performance
        var expiredKeys = _cache
            .AsParallel()
            .Where(kvp => kvp.Value.IsExpired(now))
            .Select(kvp => kvp.Key)
            .ToList();

        // Remove expired entries
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        // Clean up unused local locks (no waiters)
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

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired(now))
        {
            // Update last accessed time using CAS pattern
            if (entry.SlidingExpiration.HasValue)
            {
                var newEntry = entry.WithUpdatedAccess(now);
                _cache.TryUpdate(prefixedKey, newEntry, entry);
            }
            return entry.Data.ToArray();
        }

        return null;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var entry = new CacheEntry(value, options);
        _cache[prefixedKey] = entry;
    }

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired(now))
        {
            if (entry.SlidingExpiration.HasValue)
            {
                var newEntry = entry.WithUpdatedAccess(now);
                _cache.TryUpdate(prefixedKey, newEntry, entry);
            }
        }
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        _cache.TryRemove(prefixedKey, out _);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<Optional<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var data = Get(key);
        if (data == null || data.Length == 0)
        {
            return ValueTask.FromResult<Optional<T>>(Optional.None);
        }

        var value = _serializer.Deserialize<T>(data);
        return ValueTask.FromResult(value is not null ? Optional.Of(value) : Optional.None);
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        // First attempt: check cache
        var result = await GetAsync<T>(key, cancellationToken);
        if (result.HasValue)
        {
            return result.Value;
        }

        // Local lock to prevent multiple threads from competing
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            result = await GetAsync<T>(key, cancellationToken);
            if (result.HasValue)
            {
                return result.Value;
            }

            // Generate and cache the value
            var value = await factory(cancellationToken);
            await SetAsync(key, value, options, cancellationToken);
            return value;
        }
        finally
        {
            localLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = _serializer.Serialize(value);
        var distributedOptions = options ?? _options.DefaultEntryOptions;
        Set(key, data.ToArray(), distributedOptions);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var now = DateTimeOffset.UtcNow;
        var exists = _cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired(now);
        return ValueTask.FromResult(exists);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Optional<T>>();
        var now = DateTimeOffset.UtcNow;

        foreach (var key in keys)
        {
            var prefixedKey = ApplyKeyPrefix(key);
            // Direct cache access without updating sliding expiration (like Redis MGET)
            if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired(now))
            {
                var value = _serializer.Deserialize<T>(entry.Data.ToArray());
                result[key] = value is not null ? Optional.Of(value) : Optional.None;
            }
            else
            {
                result[key] = Optional.None;
            }
        }
        return ValueTask.FromResult<IReadOnlyDictionary<string, Optional<T>>>(result);
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var distributedOptions = options ?? _options.DefaultEntryOptions;
        foreach (var item in items)
        {
            var data = _serializer.Serialize(item.Value);
            Set(item.Key, data.ToArray(), distributedOptions);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        long count = 0;
        foreach (var key in keys)
        {
            var prefixedKey = ApplyKeyPrefix(key);
            if (_cache.TryRemove(prefixedKey, out _))
            {
                count++;
            }
        }
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        long newValue;

        // Use CAS pattern for atomic increment
        while (true)
        {
            if (_cache.TryGetValue(prefixedKey, out var oldEntry))
            {
                // Defensive check: ensure data is valid long (8 bytes)
                if (oldEntry.Data.Length != sizeof(long))
                {
                    throw new InvalidOperationException($"Cannot increment key '{key}': stored value is not a valid long integer (expected {sizeof(long)} bytes, got {oldEntry.Data.Length} bytes).");
                }

                var currentValue = BitConverter.ToInt64(oldEntry.Data.Span);
                newValue = currentValue + value;
                var newEntry = new CacheEntry(BitConverter.GetBytes(newValue), new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = oldEntry.AbsoluteExpiration,
                    SlidingExpiration = oldEntry.SlidingExpiration
                });

                if (_cache.TryUpdate(prefixedKey, newEntry, oldEntry))
                {
                    break; // Successfully updated
                }
                // CAS failed, retry
            }
            else
            {
                // Key doesn't exist, try to add it
                newValue = value;
                var newEntry = new CacheEntry(BitConverter.GetBytes(newValue), new DistributedCacheEntryOptions());
                if (_cache.TryAdd(prefixedKey, newEntry))
                {
                    break; // Successfully added
                }
                // Another thread added it, retry the update path
            }
        }

        return ValueTask.FromResult(newValue);
    }

    /// <inheritdoc />
    public ValueTask<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        return IncrementAsync(key, -value, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = _serializer.Serialize(value);
        var distributedOptions = options ?? _options.DefaultEntryOptions;
        var entry = new CacheEntry(data.ToArray(), distributedOptions);
        var added = _cache.TryAdd(prefixedKey, entry);
        return ValueTask.FromResult(added);
    }

    /// <inheritdoc />
    public ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = _serializer.Serialize(newValue);
        var distributedOptions = options ?? _options.DefaultEntryOptions;
        var newEntry = new CacheEntry(data.ToArray(), distributedOptions);
        var now = DateTimeOffset.UtcNow;

        Optional<T> oldValue = Optional.None;

        _cache.AddOrUpdate(
            prefixedKey,
            _ => newEntry,
            (_, existingEntry) =>
            {
                // Check expiration atomically inside the update callback
                if (!existingEntry.IsExpired(now))
                {
                    oldValue = Optional.Of(_serializer.Deserialize<T>(existingEntry.Data.ToArray()))!;
                }
                return newEntry;
            });

        return ValueTask.FromResult(oldValue);
    }

    /// <inheritdoc />
    public ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        if (_cache.TryRemove(prefixedKey, out var entry) && !entry.IsExpired(DateTimeOffset.UtcNow))
        {
            var value = _serializer.Deserialize<T>(entry.Data.ToArray());
            return ValueTask.FromResult(value is not null ? Optional.Of(value) : Optional.None);
        }

        return ValueTask.FromResult<Optional<T>>(Optional.None);
    }

    /// <inheritdoc />
    public ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired(now))
        {
            var ttl = entry.GetTimeToLive(now);
            return ValueTask.FromResult(ttl.HasValue ? Optional.Of(ttl.Value) : Optional.None);
        }
        return ValueTask.FromResult<Optional<TimeSpan>>(Optional.None);
    }

    /// <inheritdoc />
    public ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);

        while (_cache.TryGetValue(prefixedKey, out var oldEntry))
        {
            // Don't allow changing expiration type (absolute vs sliding)
            if (oldEntry.SlidingExpiration.HasValue)
            {
                throw new InvalidOperationException("Cannot set absolute expiration on entry with sliding expiration. Remove and re-add the entry instead.");
            }

            var newEntry = oldEntry.WithNewExpiration(DateTimeOffset.UtcNow.Add(expiration));
            if (_cache.TryUpdate(prefixedKey, newEntry, oldEntry))
            {
                return ValueTask.FromResult(true);
            }
            // CAS failed, retry
        }

        return ValueTask.FromResult(false);
    }

    private record LockKey(string Key) : CacheKey<CacheEntry>($"__nof_distributed_lock__:{Key}");

    /// <inheritdoc />
    public async ValueTask<IDistributedLock> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var lockKey = new LockKey(key);
        var prefixedKey = ApplyKeyPrefix(lockKey.Key);
        var lockId = Guid.NewGuid().ToString();
        var retryDelay = _lockRetryStrategy.InitialDelayMs;
        var attemptNumber = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_cache.TryAdd(prefixedKey, new CacheEntry(System.Text.Encoding.UTF8.GetBytes(lockId), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            })))
            {
                return new MemoryDistributedLock(this, prefixedKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor);
            }

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = _lockRetryStrategy.GetNextDelay(retryDelay, attemptNumber++, false);
        }

        throw new OperationCanceledException();
    }

    /// <inheritdoc />
    public async ValueTask<Optional<IDistributedLock>> TryAcquireLockAsync(string key, TimeSpan expiration, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var lockKey = new LockKey(key);
        var prefixedKey = ApplyKeyPrefix(lockKey.Key);
        var lockId = Guid.NewGuid().ToString();
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var retryDelay = _lockRetryStrategy.InitialDelayMs;
        var attemptNumber = 0;

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (_cache.TryAdd(prefixedKey, new CacheEntry(System.Text.Encoding.UTF8.GetBytes(lockId), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            })))
            {
                return Optional.Of<IDistributedLock>(new MemoryDistributedLock(this, prefixedKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor));
            }

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = _lockRetryStrategy.GetNextDelay(retryDelay, attemptNumber++, true);
        }

        return Optional.None;
    }

    /// <inheritdoc />
    public ValueTask ExecuteRawAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ExecuteRawAsync is not supported for in-memory cache. This method is only available for cache implementations that support raw command execution (e.g., Redis).");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _expirationTimer.Dispose();
        _cache.Clear();

        foreach (var (_, semaphore) in _localLocks)
        {
            semaphore.Dispose();
        }
        _localLocks.Clear();
    }

    private sealed class CacheEntry
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

            if (options.AbsoluteExpiration.HasValue)
            {
                AbsoluteExpiration = options.AbsoluteExpiration.Value;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
        }

        public bool IsExpired(DateTimeOffset now)
        {
            if (AbsoluteExpiration.HasValue && now >= AbsoluteExpiration.Value)
            {
                return true;
            }

            if (SlidingExpiration.HasValue && now - _lastAccessed >= SlidingExpiration.Value)
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
            if (AbsoluteExpiration.HasValue)
            {
                var ttl = AbsoluteExpiration.Value - now;
                return ttl > TimeSpan.Zero ? ttl : null;
            }

            if (SlidingExpiration.HasValue)
            {
                var ttl = SlidingExpiration.Value - (now - _lastAccessed);
                return ttl > TimeSpan.Zero ? ttl : null;
            }

            return null;
        }
    }

    private sealed class MemoryDistributedLock : IDistributedLock
    {
        private readonly MemoryCacheService _cache;
        private readonly string _lockId;
        private readonly TimeSpan _expiration;
        private readonly Task? _renewalTask;
        private readonly CancellationTokenSource? _renewalCts;
        private int _isReleased;

        public MemoryDistributedLock(MemoryCacheService cache, string key, string lockId, TimeSpan expiration, TimeSpan minimumRenewalDuration, double renewalIntervalFactor)
        {
            _cache = cache;
            Key = key;
            _lockId = lockId;
            _expiration = expiration;
            _isReleased = 0;

            // Auto-renewal: renew at configured interval factor
            if (expiration > minimumRenewalDuration)
            {
                var renewalInterval = TimeSpan.FromMilliseconds(expiration.TotalMilliseconds * renewalIntervalFactor);
                _renewalCts = new CancellationTokenSource();
                _renewalTask = RenewLockLoopAsync(renewalInterval, _renewalCts.Token);
            }
        }

        public string Key { get; }

        public bool IsAcquired => Interlocked.CompareExchange(ref _isReleased, 0, 0) == 0;

        private async Task RenewLockLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    if (Interlocked.CompareExchange(ref _isReleased, 0, 0) == 1)
                    {
                        break;
                    }

                    try
                    {
                        // Renew lock only if we still own it using CAS pattern
                        while (_cache._cache.TryGetValue(Key, out var oldEntry))
                        {
                            var storedLockId = System.Text.Encoding.UTF8.GetString(oldEntry.Data.Span);
                            if (storedLockId != _lockId)
                            {
                                break; // Lock stolen or expired
                            }

                            var newEntry = oldEntry.WithNewExpiration(DateTimeOffset.UtcNow.Add(_expiration));
                            if (_cache._cache.TryUpdate(Key, newEntry, oldEntry))
                            {
                                break; // Successfully renewed
                            }
                            // CAS failed, retry
                        }
                    }
                    catch
                    {
                        // Ignore renewal failures
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        public async ValueTask<bool> ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _isReleased, 1) == 1)
            {
                return false;
            }

            if (_renewalCts is not null)
            {
                await _renewalCts.CancelAsync();
            }
            if (_renewalTask is not null)
            {
                try
                {
                    await _renewalTask;
                }
                catch
                {
                    // Ignore cancellation exceptions
                }
            }
            _renewalCts?.Dispose();

            if (_cache._cache.TryGetValue(Key, out var entry))
            {
                var storedLockId = System.Text.Encoding.UTF8.GetString(entry.Data.Span);
                if (storedLockId == _lockId)
                {
                    _cache._cache.TryRemove(Key, out _);
                    return true;
                }
            }

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _isReleased, 0, 0) == 0)
            {
                await ReleaseAsync();
            }
        }
    }
}
