using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Per-name value configuration for a named <see cref="ICacheService"/> registration.
/// Serializer and lock retry strategy are registered as keyed DI services via
/// <c>WithSerializer</c> / <c>WithLockRetryStrategy</c> on <see cref="ICacheServiceBuilder"/>.
/// </summary>
public class CacheServiceOptions
{
    /// <summary>
    /// Gets or sets the default cache key prefix applied to all keys.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the default cache entry options.
    /// </summary>
    public DistributedCacheEntryOptions DefaultEntryOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the minimum lock duration for enabling automatic renewal.
    /// Default is 2 seconds.
    /// </summary>
    public TimeSpan MinimumLockRenewalDuration { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the lock renewal interval factor (0.0 to 1.0).
    /// The renewal interval is calculated as: expiration * factor. Default is 0.5.
    /// </summary>
    public double LockRenewalIntervalFactor { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets additional custom properties for implementation-specific configurations.
    /// </summary>
    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
}
