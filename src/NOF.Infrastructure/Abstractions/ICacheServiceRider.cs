using Microsoft.Extensions.Caching.Distributed;
using NOF.Contract;

namespace NOF.Infrastructure;

public interface ICacheServiceRider : IDistributedCache
{
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    ValueTask SetManyAsync(IDictionary<string, byte[]> items, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);

    ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    ValueTask<long> IncrementAsync(string key, long delta, CancellationToken cancellationToken = default);

    ValueTask<long> DecrementAsync(string key, long delta, CancellationToken cancellationToken = default);

    ValueTask<bool> SetIfNotExistsAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);

    ValueTask<byte[]?> GetAndSetAsync(string key, byte[] newValue, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);

    ValueTask<byte[]?> GetAndRemoveAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    ValueTask<bool> TryAcquireLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default);

    ValueTask<bool> RenewLockAsync(string key, string lockId, TimeSpan expiration, CancellationToken cancellationToken = default);

    ValueTask<bool> ReleaseLockAsync(string key, string lockId, CancellationToken cancellationToken = default);
}
