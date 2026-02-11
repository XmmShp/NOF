using Microsoft.Extensions.Caching.Distributed;

namespace NOF.Infrastructure.Core;

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
    public ICacheLockRetryStrategy? LockRetryStrategy { get; set; }

    /// <summary>
    /// Gets or sets the factory function to create a lock retry strategy.
    /// </summary>
    public Func<IServiceProvider, ICacheLockRetryStrategy>? LockRetryStrategyFactory { get; set; }

    /// <summary>
    /// Gets or sets additional custom properties for implementation-specific configurations.
    /// </summary>
    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Sets a custom serializer.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions UseSerializer(ICacheSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        Serializer = serializer;
        SerializerFactory = null;
        return this;
    }

    /// <summary>
    /// Sets a custom serializer factory.
    /// </summary>
    /// <param name="serializerFactory">The factory function to create the serializer.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions UseSerializer(Func<IServiceProvider, ICacheSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(serializerFactory);

        SerializerFactory = serializerFactory;
        Serializer = null;
        return this;
    }

    /// <summary>
    /// Sets the default key prefix for all cache operations.
    /// </summary>
    /// <param name="prefix">The key prefix.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions WithKeyPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        KeyPrefix = prefix;
        return this;
    }

    /// <summary>
    /// Sets the default cache entry options.
    /// </summary>
    /// <param name="entryOptions">The default cache entry options.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions WithDefaultOptions(DistributedCacheEntryOptions entryOptions)
    {
        ArgumentNullException.ThrowIfNull(entryOptions);

        DefaultEntryOptions = entryOptions;
        return this;
    }

    /// <summary>
    /// Sets the default cache entry options using a configuration action.
    /// </summary>
    /// <param name="configure">The action to configure the options.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions WithDefaultOptions(Action<DistributedCacheEntryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var entryOptions = new DistributedCacheEntryOptions();
        configure(entryOptions);
        DefaultEntryOptions = entryOptions;
        return this;
    }

    /// <summary>
    /// Sets a custom property for implementation-specific configuration.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <returns>The options for chaining.</returns>
    public CacheServiceOptions WithProperty(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Gets the configured serializer, creating a default one if none is configured.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <returns>The configured or default serializer.</returns>
    public ICacheSerializer GetSerializer(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (Serializer is not null)
        {
            return Serializer;
        }

        if (SerializerFactory is not null)
        {
            return SerializerFactory(serviceProvider);
        }

        return new JsonCacheSerializer();
    }

    /// <summary>
    /// Gets the configured lock retry strategy, creating a default one if none is configured.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <returns>The configured or default lock retry strategy.</returns>
    public ICacheLockRetryStrategy GetLockRetryStrategy(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (LockRetryStrategy is not null)
        {
            return LockRetryStrategy;
        }

        if (LockRetryStrategyFactory is not null)
        {
            return LockRetryStrategyFactory(serviceProvider);
        }

        return new ExponentialBackoffCacheLockRetryStrategy();
    }
}
