using Microsoft.Extensions.Caching.Distributed;
using NOF.Contract;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

public sealed class RedisCacheServiceRider : ICacheServiceRider
{
    private readonly IDatabase _database;

    public RedisCacheServiceRider(IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        _database = connectionMultiplexer.GetDatabase();
    }

    public byte[]? Get(string key) => _database.StringGet(key);

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => await _database.StringGetAsync(key);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var expiry = GetExpiration(options);
        _database.StringSet(key, value, expiry, false);
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var expiry = GetExpiration(options);
        await _database.StringSetAsync(key, value, expiry, false);
    }

    public void Refresh(string key) => _database.KeyExpire(key, TimeSpan.FromMinutes(20));

    public async Task RefreshAsync(string key, CancellationToken token = default)
        => await _database.KeyExpireAsync(key, TimeSpan.FromMinutes(20));

    public void Remove(string key) => _database.KeyDelete(key);

    public async Task RemoveAsync(string key, CancellationToken token = default)
        => await _database.KeyDeleteAsync(key);

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
        }
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        return await _database.KeyDeleteAsync(redisKeys);
    }

    public async ValueTask<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
        => await _database.StringIncrementAsync(key, delta);

    public async ValueTask<long> DecrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
        => await _database.StringDecrementAsync(key, delta);

    public async ValueTask<bool> SetIfNotExistsAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var expiry = GetExpiration(options);
        return await _database.StringSetAsync(key, value, expiry, When.NotExists);
    }

    public async ValueTask<byte[]?> GetAndSetAsync(string key, byte[] newValue, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var oldData = await _database.StringGetSetAsync(key, newValue);
        if (!oldData.HasValue)
        {
            return null;
        }

        var expiry = GetExpiration(options);
        if (expiry.HasValue)
        {
            await _database.KeyExpireAsync(key, expiry.Value);
        }

        return oldData;
    }

    public async ValueTask<byte[]?> GetAndRemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var data = await _database.StringGetDeleteAsync(key);
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
