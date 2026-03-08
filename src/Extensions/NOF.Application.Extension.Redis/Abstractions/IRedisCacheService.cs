using NOF.Application;
using NOF.Contract;

namespace NOF.Application.Extension.Redis;

public interface IRedisCacheService : ICacheService
{
    ValueTask<bool> HashSetAsync<T>(string key, string field, T value, CancellationToken cancellationToken = default);
    ValueTask<long> HashSetManyAsync<T>(string key, IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default);
    ValueTask<Optional<T>> HashGetAsync<T>(string key, string field, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyDictionary<string, Optional<T>>> HashGetManyAsync<T>(string key, IEnumerable<string> fields, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyDictionary<string, T>> HashGetAllAsync<T>(string key, CancellationToken cancellationToken = default);
    ValueTask<bool> HashExistsAsync(string key, string field, CancellationToken cancellationToken = default);
    ValueTask<bool> HashDeleteAsync(string key, string field, CancellationToken cancellationToken = default);

    ValueTask<bool> SetAddAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<bool> SetContainsAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<bool> SetRemoveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> SetMembersAsync<T>(string key, CancellationToken cancellationToken = default);
    ValueTask<long> SetLengthAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<long> ListRightPushAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<Optional<T>> ListLeftPopAsync<T>(string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1, CancellationToken cancellationToken = default);
    ValueTask<long> ListLengthAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<bool> SortedSetAddAsync<T>(string key, T value, double score, CancellationToken cancellationToken = default);
    ValueTask<long> SortedSetRemoveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> SortedSetRangeByRankAsync<T>(string key, long start = 0, long stop = -1, bool descending = false, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> SortedSetRangeByScoreAsync<T>(string key, double start = double.NegativeInfinity, double stop = double.PositiveInfinity, bool descending = false, CancellationToken cancellationToken = default);
    ValueTask<Optional<double>> SortedSetScoreAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask<double> SortedSetIncrementScoreAsync<T>(string key, T value, double delta, CancellationToken cancellationToken = default);

    async ValueTask<bool> HashSetAsync<T>(CacheKey<T> key, string field, T value, CancellationToken cancellationToken = default)
        => await HashSetAsync(key.Key, field, value, cancellationToken);

    async ValueTask<long> HashSetManyAsync<T>(CacheKey<T> key, IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default)
        => await HashSetManyAsync(key.Key, values, cancellationToken);

    async ValueTask<Optional<T>> HashGetAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
        => await HashGetAsync<T>(key.Key, field, cancellationToken);

    async ValueTask<IReadOnlyDictionary<string, Optional<T>>> HashGetManyAsync<T>(CacheKey<T> key, IEnumerable<string> fields, CancellationToken cancellationToken = default)
        => await HashGetManyAsync<T>(key.Key, fields, cancellationToken);

    async ValueTask<IReadOnlyDictionary<string, T>> HashGetAllAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
        => await HashGetAllAsync<T>(key.Key, cancellationToken);

    async ValueTask<bool> HashExistsAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
        => await HashExistsAsync(key.Key, field, cancellationToken);

    async ValueTask<bool> HashDeleteAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
        => await HashDeleteAsync(key.Key, field, cancellationToken);

    async ValueTask<bool> SetAddAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await SetAddAsync(key.Key, value, cancellationToken);

    async ValueTask<bool> SetContainsAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await SetContainsAsync(key.Key, value, cancellationToken);

    async ValueTask<bool> SetRemoveAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await SetRemoveAsync(key.Key, value, cancellationToken);

    async ValueTask<IReadOnlyList<T>> SetMembersAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
        => await SetMembersAsync<T>(key.Key, cancellationToken);

    async ValueTask<long> SetLengthAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
        => await SetLengthAsync(key.Key, cancellationToken);

    async ValueTask<long> ListRightPushAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await ListRightPushAsync(key.Key, value, cancellationToken);

    async ValueTask<Optional<T>> ListLeftPopAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
        => await ListLeftPopAsync<T>(key.Key, cancellationToken);

    async ValueTask<IReadOnlyList<T>> ListRangeAsync<T>(CacheKey<T> key, long start = 0, long stop = -1, CancellationToken cancellationToken = default)
        => await ListRangeAsync<T>(key.Key, start, stop, cancellationToken);

    async ValueTask<long> ListLengthAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
        => await ListLengthAsync(key.Key, cancellationToken);

    async ValueTask<bool> SortedSetAddAsync<T>(CacheKey<T> key, T value, double score, CancellationToken cancellationToken = default)
        => await SortedSetAddAsync(key.Key, value, score, cancellationToken);

    async ValueTask<long> SortedSetRemoveAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await SortedSetRemoveAsync(key.Key, value, cancellationToken);

    async ValueTask<IReadOnlyList<T>> SortedSetRangeByRankAsync<T>(CacheKey<T> key, long start = 0, long stop = -1, bool descending = false, CancellationToken cancellationToken = default)
        => await SortedSetRangeByRankAsync<T>(key.Key, start, stop, descending, cancellationToken);

    async ValueTask<IReadOnlyList<T>> SortedSetRangeByScoreAsync<T>(CacheKey<T> key, double start = double.NegativeInfinity, double stop = double.PositiveInfinity, bool descending = false, CancellationToken cancellationToken = default)
        => await SortedSetRangeByScoreAsync<T>(key.Key, start, stop, descending, cancellationToken);

    async ValueTask<Optional<double>> SortedSetScoreAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
        => await SortedSetScoreAsync(key.Key, value, cancellationToken);

    async ValueTask<double> SortedSetIncrementScoreAsync<T>(CacheKey<T> key, T value, double delta, CancellationToken cancellationToken = default)
        => await SortedSetIncrementScoreAsync(key.Key, value, delta, cancellationToken);
}
