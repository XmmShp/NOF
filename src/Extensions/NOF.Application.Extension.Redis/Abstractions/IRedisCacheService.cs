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
}

public static partial class NOFApplicationExtensionRedisExtensions
{
    extension(IRedisCacheService redisCacheService)
    {
        public async ValueTask<bool> HashSetAsync<T>(CacheKey<T> key, string field, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.HashSetAsync(key.Key, field, value, cancellationToken);

        public async ValueTask<long> HashSetManyAsync<T>(CacheKey<T> key, IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default)
            => await redisCacheService.HashSetManyAsync(key.Key, values, cancellationToken);

        public async ValueTask<Optional<T>> HashGetAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
            => await redisCacheService.HashGetAsync<T>(key.Key, field, cancellationToken);

        public async ValueTask<IReadOnlyDictionary<string, Optional<T>>> HashGetManyAsync<T>(CacheKey<T> key, IEnumerable<string> fields, CancellationToken cancellationToken = default)
            => await redisCacheService.HashGetManyAsync<T>(key.Key, fields, cancellationToken);

        public async ValueTask<IReadOnlyDictionary<string, T>> HashGetAllAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
            => await redisCacheService.HashGetAllAsync<T>(key.Key, cancellationToken);

        public async ValueTask<bool> HashExistsAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
            => await redisCacheService.HashExistsAsync(key.Key, field, cancellationToken);

        public async ValueTask<bool> HashDeleteAsync<T>(CacheKey<T> key, string field, CancellationToken cancellationToken = default)
            => await redisCacheService.HashDeleteAsync(key.Key, field, cancellationToken);

        public async ValueTask<bool> SetAddAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.SetAddAsync(key.Key, value, cancellationToken);

        public async ValueTask<bool> SetContainsAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.SetContainsAsync(key.Key, value, cancellationToken);

        public async ValueTask<bool> SetRemoveAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.SetRemoveAsync(key.Key, value, cancellationToken);

        public async ValueTask<IReadOnlyList<T>> SetMembersAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
            => await redisCacheService.SetMembersAsync<T>(key.Key, cancellationToken);

        public async ValueTask<long> SetLengthAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
            => await redisCacheService.SetLengthAsync(key.Key, cancellationToken);

        public async ValueTask<long> ListRightPushAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.ListRightPushAsync(key.Key, value, cancellationToken);

        public async ValueTask<Optional<T>> ListLeftPopAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
            => await redisCacheService.ListLeftPopAsync<T>(key.Key, cancellationToken);

        public async ValueTask<IReadOnlyList<T>> ListRangeAsync<T>(CacheKey<T> key, long start = 0, long stop = -1, CancellationToken cancellationToken = default)
            => await redisCacheService.ListRangeAsync<T>(key.Key, start, stop, cancellationToken);

        public async ValueTask<long> ListLengthAsync<T>(CacheKey<T> key, CancellationToken cancellationToken = default)
            => await redisCacheService.ListLengthAsync(key.Key, cancellationToken);

        public async ValueTask<bool> SortedSetAddAsync<T>(CacheKey<T> key, T value, double score, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetAddAsync(key.Key, value, score, cancellationToken);

        public async ValueTask<long> SortedSetRemoveAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetRemoveAsync(key.Key, value, cancellationToken);

        public async ValueTask<IReadOnlyList<T>> SortedSetRangeByRankAsync<T>(CacheKey<T> key, long start = 0, long stop = -1, bool descending = false, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetRangeByRankAsync<T>(key.Key, start, stop, descending, cancellationToken);

        public async ValueTask<IReadOnlyList<T>> SortedSetRangeByScoreAsync<T>(CacheKey<T> key, double start = double.NegativeInfinity, double stop = double.PositiveInfinity, bool descending = false, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetRangeByScoreAsync<T>(key.Key, start, stop, descending, cancellationToken);

        public async ValueTask<Optional<double>> SortedSetScoreAsync<T>(CacheKey<T> key, T value, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetScoreAsync(key.Key, value, cancellationToken);

        public async ValueTask<double> SortedSetIncrementScoreAsync<T>(CacheKey<T> key, T value, double delta, CancellationToken cancellationToken = default)
            => await redisCacheService.SortedSetIncrementScoreAsync(key.Key, value, delta, cancellationToken);
    }
}
