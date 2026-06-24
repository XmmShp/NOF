using Microsoft.Extensions.Caching.Distributed;
using NOF.Contract;
using StackExchange.Redis;
using System.Text;

namespace NOF.Infrastructure.StackExchangeRedis;

public sealed class RedisCacheServiceRider : ICacheServiceRider
{
    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public RedisCacheServiceRider(IConnectionMultiplexer connectionMultiplexer, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        _database = connectionMultiplexer.GetDatabase();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public byte[]? Get(string key)
    {
        var data = _database.StringGet(key);
        if (data.HasValue)
        {
            Refresh(key);
        }

        return data.HasValue ? (byte[]?)data : null;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var data = await _database.StringGetAsync(key);
        if (data.HasValue)
        {
            await RefreshAsync(key, token);
        }

        return data.HasValue ? (byte[]?)data : null;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var expiry = GetExpiration(options);
        _database.StringSet(key, value, expiry, false);
        WriteSlidingExpirationMetadata(key, options, expiry);
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var expiry = GetExpiration(options);
        await _database.StringSetAsync(key, value, expiry, false);
        await WriteSlidingExpirationMetadataAsync(key, options, expiry);
    }

    public void Refresh(string key)
    {
        var expiry = GetRefreshExpiration(key);
        if (!expiry.HasValue)
        {
            return;
        }

        _database.KeyExpire(key, expiry.Value);
        _database.KeyExpire(GetSlidingExpirationMetadataKey(key), expiry.Value);
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        var expiry = await GetRefreshExpirationAsync(key);
        if (!expiry.HasValue)
        {
            return;
        }

        await _database.KeyExpireAsync(key, expiry.Value);
        await _database.KeyExpireAsync(GetSlidingExpirationMetadataKey(key), expiry.Value);
    }

    public void Remove(string key)
    {
        _database.KeyDelete(key);
        _database.KeyDelete(GetSlidingExpirationMetadataKey(key));
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await _database.KeyDeleteAsync(key);
        await _database.KeyDeleteAsync(GetSlidingExpirationMetadataKey(key));
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => await _database.KeyExistsAsync(key);

    public async ValueTask<IReadOnlyDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var redisKeys = keyList.Select(k => (RedisKey)k).ToArray();
        var values = await _database.StringGetAsync(redisKeys);

        var result = new Dictionary<string, byte[]?>(keyList.Count);
        for (var i = 0; i < keyList.Count; i++)
        {
            result[keyList[i]] = values[i].HasValue ? (byte[]?)values[i] : null;
            if (values[i].HasValue)
            {
                await RefreshAsync(keyList[i], cancellationToken);
            }
        }
        return result;
    }

    public async ValueTask SetManyAsync(IDictionary<string, byte[]> items, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var expiry = GetExpiration(options);
        var batch = _database.CreateBatch();
        var tasks = new List<Task>(items.Count);
        foreach (var (k, v) in items)
        {
            tasks.Add(batch.StringSetAsync(k, v, expiry, false));
            tasks.Add(EnqueueSlidingExpirationMetadata(batch, k, options, expiry));
        }
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var redisKeys = keyList.Select(k => (RedisKey)k).ToArray();
        var metadataKeys = keyList.Select(GetSlidingExpirationMetadataKey).Select(static key => (RedisKey)key).ToArray();
        var batch = _database.CreateBatch();
        var deletePrimaryTask = batch.KeyDeleteAsync(redisKeys);
        var deleteMetadataTask = batch.KeyDeleteAsync(metadataKeys);
        batch.Execute();
        var deleted = await deletePrimaryTask;
        await deleteMetadataTask;
        return deleted;
    }

    public async ValueTask<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
        => await _database.StringIncrementAsync(key, delta);

    public async ValueTask<long> DecrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
        => await _database.StringDecrementAsync(key, delta);

    public async ValueTask<bool> SetIfNotExistsAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var expiry = GetExpiration(options);
        var created = await _database.StringSetAsync(key, value, expiry, When.NotExists);
        if (created)
        {
            await WriteSlidingExpirationMetadataAsync(key, options, expiry);
        }

        return created;
    }

    public async ValueTask<byte[]?> GetAndSetAsync(string key, byte[] newValue, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var oldData = await _database.StringGetSetAsync(key, newValue);
        var expiry = GetExpiration(options);
        if (expiry.HasValue)
        {
            await _database.KeyExpireAsync(key, expiry.Value);
        }
        await WriteSlidingExpirationMetadataAsync(key, options, expiry);
        if (!oldData.HasValue)
        {
            return null;
        }

        return oldData;
    }

    public async ValueTask<byte[]?> GetAndRemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var data = await _database.StringGetDeleteAsync(key);
        await _database.KeyDeleteAsync(GetSlidingExpirationMetadataKey(key));
        return data.HasValue ? (byte[]?)data : null;
    }

    public async ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var ttl = await _database.KeyTimeToLiveAsync(key);
        return ttl.HasValue ? Optional.Of(ttl.Value) : Optional.None;
    }

    public async ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
        => await _database.KeyExpireAsync(key, expiration);

    public async ValueTask<bool> TryAcquireLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        return await _database.StringSetAsync(key, lockId, expiration, When.NotExists);
    }

    public async ValueTask<bool> RenewLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        const string script = """
                              if redis.call('get', KEYS[1]) == ARGV[1] then
                                  return redis.call('pexpire', KEYS[1], ARGV[2])
                              else
                                  return 0
                              end
                              """;

        var result = await _database.ScriptEvaluateAsync(script, [key], [lockId, (long)expiration.TotalMilliseconds]);
        return (int)result == 1;
    }

    public async ValueTask<IReadOnlyDictionary<string, byte[]?>> HashGetAllAsync(string key, CancellationToken cancellationToken = default)
    {
        var entries = await _database.HashGetAllAsync(key);
        var result = new Dictionary<string, byte[]?>(entries.Length);
        foreach (var entry in entries)
        {
            result[entry.Name!] = entry.Value.HasValue ? (byte[]?)entry.Value : null;
        }
        return result;
    }

    public async ValueTask<bool> HashExistsAsync(string key, string field, CancellationToken cancellationToken = default)
        => await _database.HashExistsAsync(key, field);

    public async ValueTask<bool> HashSetAsync(string key, string field, byte[] value, CancellationToken cancellationToken = default)
        => await _database.HashSetAsync(key, field, value);

    public async ValueTask<long> HashSetManyAsync(string key, IReadOnlyDictionary<string, byte[]> values, CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        var entries = values.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
        await _database.HashSetAsync(key, entries);
        return entries.Length;
    }

    public async ValueTask<byte[]?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        var data = await _database.HashGetAsync(key, field);
        return data.HasValue ? (byte[]?)data : null;
    }

    public async ValueTask<IReadOnlyDictionary<string, byte[]?>> HashGetManyAsync(string key, IEnumerable<string> fields, CancellationToken cancellationToken = default)
    {
        var fieldList = fields.ToList();
        var values = await _database.HashGetAsync(key, fieldList.Select(f => (RedisValue)f).ToArray());
        var result = new Dictionary<string, byte[]?>(fieldList.Count);
        for (var i = 0; i < fieldList.Count; i++)
        {
            result[fieldList[i]] = values[i].HasValue ? (byte[]?)values[i] : null;
        }
        return result;
    }

    public async ValueTask<bool> HashDeleteAsync(string key, string field, CancellationToken cancellationToken = default)
        => await _database.HashDeleteAsync(key, field);

    public async ValueTask<bool> SetAddAsync(string key, byte[] value, CancellationToken cancellationToken = default)
        => await _database.SetAddAsync(key, value);

    public async ValueTask<bool> SetContainsAsync(string key, byte[] value, CancellationToken cancellationToken = default)
        => await _database.SetContainsAsync(key, value);

    public async ValueTask<bool> SetRemoveAsync(string key, byte[] value, CancellationToken cancellationToken = default)
        => await _database.SetRemoveAsync(key, value);

    public async ValueTask<IReadOnlyList<byte[]>> SetMembersAsync(string key, CancellationToken cancellationToken = default)
    {
        var values = await _database.SetMembersAsync(key);
        return ToByteArrayList(values);
    }

    public async ValueTask<long> SetLengthAsync(string key, CancellationToken cancellationToken = default)
        => await _database.SetLengthAsync(key);

    public async ValueTask<long> ListRightPushAsync(string key, byte[] value, CancellationToken cancellationToken = default)
        => await _database.ListRightPushAsync(key, value);

    public async ValueTask<byte[]?> ListLeftPopAsync(string key, CancellationToken cancellationToken = default)
    {
        var data = await _database.ListLeftPopAsync(key);
        return data.HasValue ? (byte[]?)data : null;
    }

    public async ValueTask<IReadOnlyList<byte[]>> ListRangeAsync(string key, long start = 0, long stop = -1, CancellationToken cancellationToken = default)
    {
        var values = await _database.ListRangeAsync(key, start, stop);
        return ToByteArrayList(values);
    }

    public async ValueTask<long> ListLengthAsync(string key, CancellationToken cancellationToken = default)
        => await _database.ListLengthAsync(key);

    public async ValueTask<bool> SortedSetAddAsync(string key, byte[] value, double score, CancellationToken cancellationToken = default)
        => await _database.SortedSetAddAsync(key, value, score);

    public async ValueTask<long> SortedSetRemoveAsync(string key, byte[] value, CancellationToken cancellationToken = default)
        => await _database.SortedSetRemoveAsync(key, value) ? 1 : 0;

    public async ValueTask<IReadOnlyList<byte[]>> SortedSetRangeByRankAsync(string key, long start = 0, long stop = -1, bool descending = false, CancellationToken cancellationToken = default)
    {
        var values = await _database.SortedSetRangeByRankAsync(key, start, stop, descending ? Order.Descending : Order.Ascending);
        return ToByteArrayList(values);
    }

    public async ValueTask<IReadOnlyList<byte[]>> SortedSetRangeByScoreAsync(string key, double start = double.NegativeInfinity, double stop = double.PositiveInfinity, bool descending = false, CancellationToken cancellationToken = default)
    {
        var values = await _database.SortedSetRangeByScoreAsync(key, start, stop, order: descending ? Order.Descending : Order.Ascending);
        return ToByteArrayList(values);
    }

    public async ValueTask<double?> SortedSetScoreAsync(string key, byte[] value, CancellationToken cancellationToken = default)
    {
        var score = await _database.SortedSetScoreAsync(key, value);
        return score;
    }

    public async ValueTask<double> SortedSetIncrementScoreAsync(string key, byte[] value, double delta, CancellationToken cancellationToken = default)
        => await _database.SortedSetIncrementAsync(key, value, delta);

    public async ValueTask<bool> ReleaseLockAsync(string key, string lockId, CancellationToken cancellationToken = default)
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

    private TimeSpan? GetRefreshExpiration(string key)
    {
        var metadataValue = _database.StringGet(GetSlidingExpirationMetadataKey(key));
        if (!metadataValue.HasValue || !TryParseSlidingExpirationMetadata(metadataValue!, out var metadata))
        {
            return null;
        }

        return GetRefreshExpiration(metadata);
    }

    private async Task<TimeSpan?> GetRefreshExpirationAsync(string key)
    {
        var metadataValue = await _database.StringGetAsync(GetSlidingExpirationMetadataKey(key));
        if (!metadataValue.HasValue || !TryParseSlidingExpirationMetadata(metadataValue!, out var metadata))
        {
            return null;
        }

        return GetRefreshExpiration(metadata);
    }

    private TimeSpan? GetRefreshExpiration(SlidingExpirationMetadata metadata)
    {
        var now = _timeProvider.GetUtcNow();
        var expiration = metadata.AbsoluteExpirationUtc is null
            ? metadata.SlidingExpiration
            : Min(metadata.SlidingExpiration, metadata.AbsoluteExpirationUtc.Value - now);

        if (expiration <= TimeSpan.Zero)
        {
            return null;
        }

        return expiration;
    }

    private void WriteSlidingExpirationMetadata(string key, DistributedCacheEntryOptions options, TimeSpan? expiry)
    {
        var metadataKey = GetSlidingExpirationMetadataKey(key);
        if (!TryCreateSlidingExpirationMetadata(options, out var metadata))
        {
            _database.KeyDelete(metadataKey);
            return;
        }

        _database.StringSet(metadataKey, SerializeSlidingExpirationMetadata(metadata), expiry, false);
    }

    private async Task WriteSlidingExpirationMetadataAsync(string key, DistributedCacheEntryOptions options, TimeSpan? expiry)
    {
        var metadataKey = GetSlidingExpirationMetadataKey(key);
        if (!TryCreateSlidingExpirationMetadata(options, out var metadata))
        {
            await _database.KeyDeleteAsync(metadataKey);
            return;
        }

        await _database.StringSetAsync(metadataKey, SerializeSlidingExpirationMetadata(metadata), expiry, false);
    }

    private Task EnqueueSlidingExpirationMetadata(IBatch batch, string key, DistributedCacheEntryOptions options, TimeSpan? expiry)
    {
        var metadataKey = GetSlidingExpirationMetadataKey(key);
        if (!TryCreateSlidingExpirationMetadata(options, out var metadata))
        {
            return batch.KeyDeleteAsync(metadataKey);
        }

        return batch.StringSetAsync(metadataKey, SerializeSlidingExpirationMetadata(metadata), expiry, false);
    }

    private bool TryCreateSlidingExpirationMetadata(DistributedCacheEntryOptions options, out SlidingExpirationMetadata metadata)
    {
        if (!options.SlidingExpiration.HasValue)
        {
            metadata = default;
            return false;
        }

        metadata = new SlidingExpirationMetadata(GetAbsoluteExpiration(options), options.SlidingExpiration.Value);
        return true;
    }

    private DateTimeOffset? GetAbsoluteExpiration(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpiration.HasValue)
        {
            return options.AbsoluteExpiration.Value;
        }

        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return _timeProvider.GetUtcNow().Add(options.AbsoluteExpirationRelativeToNow.Value);
        }

        return null;
    }

    private static string GetSlidingExpirationMetadataKey(string key)
        => $"{key}::__nof:sliding-expiration";

    private static byte[] SerializeSlidingExpirationMetadata(SlidingExpirationMetadata metadata)
        => Encoding.UTF8.GetBytes($"{metadata.SlidingExpiration.Ticks}|{metadata.AbsoluteExpirationUtc?.UtcDateTime.Ticks.ToString() ?? string.Empty}");

    private static bool TryParseSlidingExpirationMetadata(byte[] data, out SlidingExpirationMetadata metadata)
    {
        var parts = Encoding.UTF8.GetString(data).Split('|', 2, StringSplitOptions.None);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var slidingTicks))
        {
            metadata = default;
            return false;
        }

        DateTimeOffset? absoluteExpiration = null;
        if (!string.IsNullOrWhiteSpace(parts[1]))
        {
            if (!long.TryParse(parts[1], out var absoluteTicks))
            {
                metadata = default;
                return false;
            }

            absoluteExpiration = new DateTimeOffset(absoluteTicks, TimeSpan.Zero);
        }

        metadata = new SlidingExpirationMetadata(absoluteExpiration, TimeSpan.FromTicks(slidingTicks));
        return true;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
        => left <= right ? left : right;

    private readonly record struct SlidingExpirationMetadata(DateTimeOffset? AbsoluteExpirationUtc, TimeSpan SlidingExpiration);

    private static IReadOnlyList<byte[]> ToByteArrayList(RedisValue[] values)
    {
        var result = new List<byte[]>(values.Length);
        foreach (var value in values)
        {
            if (value.HasValue)
            {
                result.Add(value!);
            }
        }
        return result;
    }

}
