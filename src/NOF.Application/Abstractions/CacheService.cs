using Microsoft.Extensions.Caching.Distributed;

namespace NOF;

/// <summary>
/// Provides caching operations with support for distributed locking and transactions.
/// Extends <see cref="IDistributedCache"/> for compatibility with .NET ecosystem.
/// </summary>
public interface ICacheService : IDistributedCache
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An optional containing the cached value if found.</returns>
    ValueTask<Optional<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached value or sets it using the factory if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the value if cache misses.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created value.</returns>
    ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple cached values by their keys.
    /// </summary>
    /// <typeparam name="T">The type of the cached values.</typeparam>
    /// <param name="keys">The cache keys.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping keys to optional values.</returns>
    ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets multiple values in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the values to cache.</typeparam>
    /// <param name="items">Dictionary of keys and values to cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple cached values by their keys.
    /// </summary>
    /// <param name="keys">The cache keys to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of keys removed.</returns>
    ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a numeric value in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to increment by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new value after incrementing.</returns>
    ValueTask<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements a numeric value in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to decrement by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new value after decrementing.</returns>
    ValueTask<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache only if the key does not already exist.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the value was set; false if the key already existed.</returns>
    ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically gets the current value and sets a new value.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="newValue">The new value to set.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An optional containing the previous value if it existed.</returns>
    ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically gets and removes a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An optional containing the value if it existed.</returns>
    ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the remaining time to live for a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An optional containing the remaining TTL if the key exists.</returns>
    ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the time to live for a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="expiration">The expiration time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the TTL was set; otherwise, false.</returns>
    ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a distributed lock, waiting indefinitely until acquired.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="expiration">The lock expiration time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acquired distributed lock.</returns>
    ValueTask<IDistributedLock> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a distributed lock within the specified timeout.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="expiration">The lock expiration time.</param>
    /// <param name="timeout">The maximum time to wait for the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An optional containing the lock if acquired within the timeout.</returns>
    ValueTask<Optional<IDistributedLock>> TryAcquireLockAsync(string key, TimeSpan expiration, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a distributed lock that can be released asynchronously.
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// Gets the lock key.
    /// </summary>
    string Key { get; }
    /// <summary>
    /// Gets a value indicating whether the lock is currently acquired.
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// Releases the distributed lock.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock was released; otherwise, false.</returns>
    ValueTask<bool> ReleaseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Extends <see cref="ICacheService"/> with low-level cache engine operations.
/// <para>
/// <strong>WARNING:</strong> This interface breaks encapsulation and exposes implementation details.
/// Use this ONLY when you need direct access to the underlying cache engine for advanced scenarios.
/// Prefer using standard <see cref="ICacheService"/> methods whenever possible.
/// </para>
/// </summary>
public interface ICacheServiceWithRawAccess : ICacheService
{
    /// <summary>
    /// Executes a raw engine-specific operation.
    /// <para>
    /// <strong>DANGER:</strong> This method bypasses all abstractions and directly interacts with the cache engine.
    /// The parameters dictionary is engine-specific and may vary between implementations (Redis, Memcached, etc.).
    /// Using this method makes your code tightly coupled to a specific cache implementation.
    /// </para>
    /// </summary>
    /// <param name="parameters">Engine-specific parameters. The keys and values depend on the underlying cache implementation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask ExecuteRawAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
}