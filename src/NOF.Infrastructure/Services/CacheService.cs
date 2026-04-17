using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public sealed class CacheService : ICacheService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();

    private readonly ICacheServiceRider _rider;
    private readonly IObjectSerializer _serializer;
    private readonly ICacheLockRetryStrategy _lockRetryStrategy;
    private readonly CacheServiceOptions _options;
    private readonly IExecutionContext _executionContext;

    public CacheService(
        ICacheServiceRider rider,
        IObjectSerializer serializer,
        ICacheLockRetryStrategy lockRetryStrategy,
        IOptions<CacheServiceOptions> options,
        IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(rider);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(lockRetryStrategy);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(executionContext);

        _rider = rider;
        _serializer = serializer;
        _lockRetryStrategy = lockRetryStrategy;
        _options = options.Value;
        _executionContext = executionContext;
    }

    private string ApplyKeyPrefix(string key)
    {
        var keyPrefixTemplate = _options.KeyPrefix ?? string.Empty;
        var keyPrefix = DbConnectionStringTemplateResolver.ResolveTenantId(
            keyPrefixTemplate,
            _executionContext.TenantId);

        return keyPrefix + key;
    }

    public byte[]? Get(string key) => _rider.Get(ApplyKeyPrefix(key));

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        _rider.GetAsync(ApplyKeyPrefix(key), token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        _rider.Set(ApplyKeyPrefix(key), value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        _rider.SetAsync(ApplyKeyPrefix(key), value, options, token);

    public void Refresh(string key) => _rider.Refresh(ApplyKeyPrefix(key));

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        _rider.RefreshAsync(ApplyKeyPrefix(key), token);

    public void Remove(string key) => _rider.Remove(ApplyKeyPrefix(key));

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        _rider.RemoveAsync(ApplyKeyPrefix(key), token);

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

    public async ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<T>(key, cancellationToken);
        if (result.HasValue)
        {
            return result.Value;
        }

        var prefixedKey = ApplyKeyPrefix(key);
        var localLock = _localLocks.GetOrAdd(prefixedKey, _ => new SemaphoreSlim(1, 1));
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

    public ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = _serializer.Serialize(value);
        var distributedOptions = options ?? _options.DefaultEntryOptions;
        Set(key, data.ToArray(), distributedOptions);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        _rider.ExistsAsync(ApplyKeyPrefix(key), cancellationToken);

    public async ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var prefixedKeys = keyList.Select(ApplyKeyPrefix).ToList();
        var rawMap = await _rider.GetManyAsync(prefixedKeys, cancellationToken);

        var result = new Dictionary<string, Optional<T>>(keyList.Count);
        for (var i = 0; i < keyList.Count; i++)
        {
            var raw = rawMap[prefixedKeys[i]];
            if (raw is not null)
            {
                var value = _serializer.Deserialize<T>(raw);
                result[keyList[i]] = value is not null ? Optional.Of(value) : Optional.None;
            }
            else
            {
                result[keyList[i]] = Optional.None;
            }
        }
        return result;
    }

    public async ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var opts = options ?? _options.DefaultEntryOptions;
        var rawItems = new Dictionary<string, byte[]>(items.Count);
        foreach (var (k, v) in items)
        {
            rawItems[ApplyKeyPrefix(k)] = _serializer.Serialize(v).ToArray();
        }
        await _rider.SetManyAsync(rawItems, opts, cancellationToken);
    }

    public ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        _rider.RemoveManyAsync(keys.Select(ApplyKeyPrefix), cancellationToken);

    public ValueTask<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default) =>
        _rider.IncrementAsync(ApplyKeyPrefix(key), value, cancellationToken);

    public ValueTask<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default) =>
        _rider.DecrementAsync(ApplyKeyPrefix(key), value, cancellationToken);

    public ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = _serializer.Serialize(value);
        var opts = options ?? _options.DefaultEntryOptions;
        return _rider.SetIfNotExistsAsync(ApplyKeyPrefix(key), data.ToArray(), opts, cancellationToken);
    }

    public async ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = _serializer.Serialize(newValue);
        var opts = options ?? _options.DefaultEntryOptions;
        var oldData = await _rider.GetAndSetAsync(ApplyKeyPrefix(key), data.ToArray(), opts, cancellationToken);
        if (oldData is null)
        {
            return Optional.None;
        }
        var oldValue = _serializer.Deserialize<T>(oldData);
        return oldValue is not null ? Optional.Of(oldValue) : Optional.None;
    }

    public async ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var oldData = await _rider.GetAndRemoveAsync(ApplyKeyPrefix(key), cancellationToken);
        if (oldData is null)
        {
            return Optional.None;
        }
        var value = _serializer.Deserialize<T>(oldData);
        return value is not null ? Optional.Of(value) : Optional.None;
    }

    public ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default) =>
        _rider.GetTimeToLiveAsync(ApplyKeyPrefix(key), cancellationToken);

    public ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default) =>
        _rider.SetTimeToLiveAsync(ApplyKeyPrefix(key), expiration, cancellationToken);

    public async ValueTask<IDistributedLock> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var lockKey = ApplyKeyPrefix($"__nof_distributed_lock__:{key}");
        var lockId = Guid.NewGuid().ToString();
        var retryDelay = _lockRetryStrategy.InitialDelayMs;
        var attemptNumber = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (await _rider.TryAcquireLockAsync(lockKey, lockId, expiration, cancellationToken))
            {
                return new CacheDistributedLock(_rider, lockKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor);
            }

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = _lockRetryStrategy.GetNextDelay(retryDelay, attemptNumber++, false);
        }

        throw new OperationCanceledException();
    }

    public async ValueTask<Optional<IDistributedLock>> TryAcquireLockAsync(string key, TimeSpan expiration, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var lockKey = ApplyKeyPrefix($"__nof_distributed_lock__:{key}");
        var lockId = Guid.NewGuid().ToString();
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var retryDelay = _lockRetryStrategy.InitialDelayMs;
        var attemptNumber = 0;

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await _rider.TryAcquireLockAsync(lockKey, lockId, expiration, cancellationToken))
            {
                return Optional.Of<IDistributedLock>(new CacheDistributedLock(_rider, lockKey, lockId, expiration, _options.MinimumLockRenewalDuration, _options.LockRenewalIntervalFactor));
            }

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = _lockRetryStrategy.GetNextDelay(retryDelay, attemptNumber++, true);
        }

        return Optional.None;
    }

    private sealed class CacheDistributedLock : IDistributedLock
    {
        private readonly ICacheServiceRider _rider;
        private readonly string _lockId;
        private readonly TimeSpan _expiration;
        private readonly Task? _renewalTask;
        private readonly CancellationTokenSource? _renewalCts;
        private int _isReleased;

        public CacheDistributedLock(
            ICacheServiceRider rider,
            string key,
            string lockId,
            TimeSpan expiration,
            TimeSpan minimumRenewalDuration,
            double renewalIntervalFactor)
        {
            _rider = rider;
            Key = key;
            _lockId = lockId;
            _expiration = expiration;

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
                        await _rider.RenewLockAsync(Key, _lockId, _expiration, cancellationToken);
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

            return await _rider.ReleaseLockAsync(Key, _lockId, cancellationToken);
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
