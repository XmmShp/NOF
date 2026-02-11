using Microsoft.Extensions.Caching.Distributed;

namespace NOF.Application;

/// <summary>
/// Extension methods for the NOF.Application layer.
/// </summary>
public static partial class NOFApplicationExtensions
{
    /// <param name="cache">Distributed cache interface</param>
    extension(IDistributedCache cache)
    {
        /// <summary>
        /// Removes the cache item corresponding to the specified key from the cache.
        /// </summary>
        /// <typeparam name="TValue">The type of the cache item (used only for type inference with strongly-typed keys, does not affect the actual removal operation).</typeparam>
        /// <param name="key">The strongly-typed key of the cache item to remove.</param>
        public void Remove<TValue>(CacheKey<TValue> key)
            => cache.Remove(key.Key);

        /// <summary>
        /// Asynchronously removes the cache item corresponding to the specified key from the cache.
        /// </summary>
        /// <typeparam name="TValue">The type of the cache item (used only for type inference with strongly-typed keys, does not affect the actual removal operation).</typeparam>
        /// <param name="key">The strongly-typed key of the cache item to remove.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous removal operation.</returns>
        public async ValueTask RemoveAsync<TValue>(CacheKey<TValue> key, CancellationToken token = default)
            => await cache.RemoveAsync(key.Key, token);

        /// <summary>
        /// Refreshes the sliding expiration time of the specified cache item (if applicable), preventing it from being removed due to expiration.
        /// </summary>
        /// <typeparam name="TValue">The type of the cache item (used only for type inference with strongly-typed keys, does not affect the actual refresh operation).</typeparam>
        /// <param name="key">The strongly-typed key of the cache item to refresh.</param>
        public void Refresh<TValue>(CacheKey<TValue> key)
            => cache.Refresh(key.Key);

        /// <summary>
        /// Asynchronously refreshes the sliding expiration time of the specified cache item (if applicable), preventing it from being removed due to expiration.
        /// </summary>
        /// <typeparam name="TValue">The type of the cache item (used only for type inference with strongly-typed keys, does not affect the actual refresh operation).</typeparam>
        /// <param name="key">The strongly-typed key of the cache item to refresh.</param>
        /// <param name="ct">The <see cref="CancellationToken"/> used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous refresh operation.</returns>
        public async ValueTask RefreshAsync<TValue>(CacheKey<TValue> key, CancellationToken ct = default)
            => await cache.RefreshAsync(key.Key, ct);
    }
}
