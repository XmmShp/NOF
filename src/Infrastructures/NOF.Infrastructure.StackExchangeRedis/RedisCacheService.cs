using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Core;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.StackExchangeRedis;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ICacheSerializer _serializer;
    private readonly ICacheLockRetryStrategy _lockRetryStrategy;
    private readonly CacheServiceOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();

    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ICacheSerializer serializer,
        IOptions<CacheServiceOptions> options,
        ICacheLockRetryStrategy lockRetryStrategy)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lockRetryStrategy);

        _database = connectionMultiplexer.GetDatabase();
        _serializer = serializer;
        _options = options.Value;
        _lockRetryStrategy = lockRetryStrategy;
    }

    private string ApplyKeyPrefix(string key)
    {
        return string.IsNullOrEmpty(_options.KeyPrefix) ? key : _options.KeyPrefix + key;
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return _database.StringGet(prefixedKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return await _database.StringGetAsync(prefixedKey);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var expiry = GetExpiration(options);
        _database.StringSet(prefixedKey, value, expiry, false);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var expiry = GetExpiration(options);
        await _database.StringSetAsync(prefixedKey, value, expiry, false);
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        _database.KeyExpire(prefixedKey, TimeSpan.FromMinutes(20));
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        await _database.KeyExpireAsync(prefixedKey, TimeSpan.FromMinutes(20));
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        _database.KeyDelete(prefixedKey);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        await _database.KeyDeleteAsync(prefixedKey);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = await _database.StringGetAsync(prefixedKey);

        if (!data.HasValue)
        {
            return Optional.None;
        }

        var value = _serializer.Deserialize<T>(data!);
        return value != null ? Optional.Of(value) : Optional.None;
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

        // Local lock to prevent multiple threads from same process competing
        var localLock = _localLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await localLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring local lock
            result = await GetAsync<T>(key, cancellationToken);
            if (result.HasValue)
            {
                return result.Value;
            }

            // Use distributed lock to prevent cache stampede across processes
            var lockKey = $"lock:getorset:{key}";
            var lockExpiration = TimeSpan.FromSeconds(30);
            var lockTimeout = TimeSpan.FromSeconds(5);

            var lockResult = await TryAcquireLockAsync(lockKey, lockExpiration, lockTimeout, cancellationToken);

            if (lockResult.HasValue)
            {
                await using var distributedLock = lockResult.Value;

                // Triple-check: another process might have set the value
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

            // Failed to acquire distributed lock, wait and retry
            await Task.Delay(100, cancellationToken);
            result = await GetAsync<T>(key, cancellationToken);
            if (result.HasValue)
            {
                return result.Value;
            }

            // Last resort: generate without lock (rare case)
            return await factory(cancellationToken);
        }
        finally
        {
            localLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = _serializer.Serialize(value);
        var expiry = GetExpiration(options ?? _options.DefaultEntryOptions);
        await _database.StringSetAsync(prefixedKey, data.ToArray(), expiry, false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return await _database.KeyExistsAsync(prefixedKey);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var prefixedKeys = keyList.Select(ApplyKeyPrefix).Select(k => (RedisKey)k).ToArray();
        var values = await _database.StringGetAsync(prefixedKeys);

        var result = new Dictionary<string, Optional<T>>();
        for (var i = 0; i < keyList.Count; i++)
        {
            if (values[i].HasValue)
            {
                var deserialized = _serializer.Deserialize<T>(values[i]!);
                result[keyList[i]] = deserialized != null ? Optional.Of(deserialized) : Optional.None;
            }
            else
            {
                result[keyList[i]] = Optional.None;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var expiry = GetExpiration(options ?? _options.DefaultEntryOptions);
        var batch = _database.CreateBatch();
        var tasks = new List<Task>();

        foreach (var item in items)
        {
            var prefixedKey = ApplyKeyPrefix(item.Key);
            var data = _serializer.Serialize(item.Value);
            tasks.Add(batch.StringSetAsync(prefixedKey, data.ToArray(), expiry, false));
        }

        batch.Execute();
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var prefixedKeys = keys.Select(ApplyKeyPrefix).Select(k => (RedisKey)k).ToArray();
        return await _database.KeyDeleteAsync(prefixedKeys);
    }

    /// <inheritdoc />
    public async ValueTask<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return await _database.StringIncrementAsync(prefixedKey, delta);
    }

    /// <inheritdoc />
    public async ValueTask<long> DecrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return await _database.StringDecrementAsync(prefixedKey, delta);
    }

    /// <inheritdoc />
    public async ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = _serializer.Serialize(value);
        var expiry = GetExpiration(options ?? _options.DefaultEntryOptions);
        return await _database.StringSetAsync(prefixedKey, data.ToArray(), expiry, When.NotExists);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = _serializer.Serialize(newValue);
        var oldData = await _database.StringGetSetAsync(prefixedKey, data.ToArray());

        if (!oldData.HasValue)
        {
            return Optional.None;
        }

        var expiry = GetExpiration(options ?? _options.DefaultEntryOptions);
        if (expiry.HasValue)
        {
            await _database.KeyExpireAsync(prefixedKey, expiry.Value);
        }

        var oldValue = _serializer.Deserialize<T>(oldData!);
        return oldValue != null ? Optional.Of(oldValue) : Optional.None;
    }

    /// <inheritdoc />
    public async ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var data = await _database.StringGetDeleteAsync(prefixedKey);

        if (!data.HasValue)
        {
            return Optional.None;
        }

        var value = _serializer.Deserialize<T>(data!);
        return value != null ? Optional.Of(value) : Optional.None;
    }

    /// <inheritdoc />
    public async ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        var ttl = await _database.KeyTimeToLiveAsync(prefixedKey);
        return ttl.HasValue ? Optional.Of(ttl.Value) : Optional.None;
    }

    /// <inheritdoc />
    public async ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var prefixedKey = ApplyKeyPrefix(key);
        return await _database.KeyExpireAsync(prefixedKey, expiration);
    }

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
            if (await _database.StringSetAsync(prefixedKey, lockId, expiration, When.NotExists))
            {
                return new RedisDistributedLock(this, _database, prefixedKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor);
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
            if (await _database.StringSetAsync(prefixedKey, lockId, expiration, When.NotExists))
            {
                return Optional.Of<IDistributedLock>(new RedisDistributedLock(this, _database, prefixedKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor));
            }

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = _lockRetryStrategy.GetNextDelay(retryDelay, attemptNumber++, true);
        }

        return Optional.None;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteRawAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Expected parameters:
        // - "Command": string - Redis command name (e.g., "SET", "GET", "HSET")
        // - "Args": object[] - Command arguments
        // - "Script": string (optional) - Lua script to execute
        // - "Keys": string[] (optional) - Keys for Lua script
        // - "Values": object[] (optional) - Values for Lua script

        if (parameters.TryGetValue("Script", out var scriptObj) && scriptObj is string script)
        {
            // Execute Lua script
            var keys = parameters.TryGetValue("Keys", out var keysObj) && keysObj is string[] keysArray
                ? keysArray.Select(k => (RedisKey)ApplyKeyPrefix(k)).ToArray()
                : [];

            var values = parameters.TryGetValue("Values", out var valuesObj) && valuesObj is object[] valuesArray
                ? valuesArray.Select(v => (RedisValue)(v.ToString() ?? string.Empty)).ToArray()
                : [];

            await _database.ScriptEvaluateAsync(script, keys, values);
        }
        else if (parameters.TryGetValue("Command", out var commandObj) && commandObj is string command)
        {
            // Execute raw Redis command
            var args = parameters.TryGetValue("Args", out var argsObj) && argsObj is object[] argsArray
                ? argsArray.Select(object (a) => a.ToString() ?? string.Empty).ToArray()
                : [];

            await _database.ExecuteAsync(command, args);
        }
        else
        {
            throw new ArgumentException("Parameters must contain either 'Script' or 'Command' key.", nameof(parameters));
        }
    }

    internal async ValueTask<bool> ReleaseLockAsync(string key, string lockId)
    {
        const string script = """
                              if redis.call('get', KEYS[1]) == ARGV[1] then
                                  return redis.call('del', KEYS[1])
                              else
                                  return 0
                              end
                              """;

        var result = await _database.ScriptEvaluateAsync(script, [key], [lockId]);
        return (int)result == 1;
    }

    private static TimeSpan? GetExpiration(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return options.AbsoluteExpirationRelativeToNow.Value;
        }

        if (options.AbsoluteExpiration.HasValue)
        {
            return options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
        }

        return options.SlidingExpiration;
    }

    private record LockKey(string Key) : CacheKey<byte[]>($"__nof_redis_distributed_lock__:{Key}");

    private sealed class RedisDistributedLock : IDistributedLock
    {
        private readonly RedisCacheService _cache;
        private readonly IDatabase _database;
        private readonly string _lockId;
        private readonly TimeSpan _expiration;
        private readonly Task? _renewalTask;
        private readonly CancellationTokenSource? _renewalCts;
        private int _isReleased;

        public RedisDistributedLock(RedisCacheService cache, IDatabase database, string key, string lockId, TimeSpan expiration, TimeSpan minimumRenewalDuration, double renewalIntervalFactor)
        {
            _cache = cache;
            _database = database;
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
                        // Renew lock only if we still own it
                        const string script = """
                                              if redis.call('get', KEYS[1]) == ARGV[1] then
                                                  return redis.call('pexpire', KEYS[1], ARGV[2])
                                              else
                                                  return 0
                                              end
                                              """;

                        await _database.ScriptEvaluateAsync(script, [Key], [_lockId, (long)_expiration.TotalMilliseconds]);
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

            return await _cache.ReleaseLockAsync(Key, _lockId);
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
