using Microsoft.Extensions.Caching.Distributed;

namespace NOF;

/// <summary>
/// Configurator for cache service options.
/// </summary>
public class CacheServiceOptions
{
    /// <summary>
    /// Gets or sets the cache serializer.
    /// </summary>
    public ICacheSerializer? Serializer { get; set; }

    /// <summary>
    /// Gets or sets the serializer factory.
    /// </summary>
    public Func<IServiceProvider, ICacheSerializer>? SerializerFactory { get; set; }

    /// <summary>
    /// Gets or sets the default cache key prefix.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the default cache entry options.
    /// </summary>
    public DistributedCacheEntryOptions DefaultEntryOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the minimum lock duration for enabling automatic renewal.
    /// Locks with expiration time greater than this value will be automatically renewed.
    /// Default is 2 seconds.
    /// </summary>
    public TimeSpan MinimumLockRenewalDuration { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the lock renewal interval factor (0.0 to 1.0).
    /// The renewal interval will be calculated as: expiration * renewalIntervalFactor.
    /// Default is 0.5 (renew at half of the expiration time).
    /// </summary>
    public double LockRenewalIntervalFactor { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the lock retry strategy.
    /// If null, a default ExponentialBackoffRetryStrategy will be created using the legacy properties.
    /// </summary>
    public ILockRetryStrategy? LockRetryStrategy { get; set; }

    /// <summary>
    /// Gets or sets the factory function to create a lock retry strategy.
    /// </summary>
    public Func<IServiceProvider, ILockRetryStrategy>? LockRetryStrategyFactory { get; set; }

    /// <summary>
    /// Gets or sets additional custom properties for implementation-specific configurations.
    /// </summary>
    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
}
