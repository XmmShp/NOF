using Microsoft.Extensions.Caching.Distributed;

namespace NOF;

public static partial class __NOF_Infrastructure_Core_Extensions__
{
    /// <param name="options">The cache service options.</param>
    extension(CacheServiceOptions options)
    {
        /// <summary>
        /// Sets a custom serializer.
        /// </summary>
        /// <param name="serializer">The serializer to use.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions UseSerializer(ICacheSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serializer);

            options.Serializer = serializer;
            options.SerializerFactory = null;
            return options;
        }

        /// <summary>
        /// Sets a custom serializer factory.
        /// </summary>
        /// <param name="serializerFactory">The factory function to create the serializer.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions UseSerializer(Func<IServiceProvider, ICacheSerializer> serializerFactory)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serializerFactory);

            options.SerializerFactory = serializerFactory;
            options.Serializer = null;
            return options;
        }

        /// <summary>
        /// Sets the default key prefix for all cache operations.
        /// </summary>
        /// <param name="prefix">The key prefix.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions WithKeyPrefix(string prefix)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(prefix);

            options.KeyPrefix = prefix;
            return options;
        }

        /// <summary>
        /// Sets the default cache entry options.
        /// </summary>
        /// <param name="entryOptions">The default cache entry options.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions WithDefaultOptions(DistributedCacheEntryOptions entryOptions)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(entryOptions);

            options.DefaultEntryOptions = entryOptions;
            return options;
        }

        /// <summary>
        /// Sets the default cache entry options using a configuration action.
        /// </summary>
        /// <param name="configure">The action to configure the options.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions WithDefaultOptions(Action<DistributedCacheEntryOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(configure);

            var entryOptions = new DistributedCacheEntryOptions();
            configure(entryOptions);
            options.DefaultEntryOptions = entryOptions;
            return options;
        }

        /// <summary>
        /// Sets a custom property for implementation-specific configuration.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        /// <returns>The options for chaining.</returns>
        public CacheServiceOptions WithProperty(string key, object? value)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(key);

            options.Properties[key] = value;
            return options;
        }

        /// <summary>
        /// Gets the configured serializer, creating a default one if none is configured.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <returns>The configured or default serializer.</returns>
        public ICacheSerializer GetSerializer(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            if (options.Serializer is not null)
            {
                return options.Serializer;
            }

            if (options.SerializerFactory is not null)
            {
                return options.SerializerFactory(serviceProvider);
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
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            if (options.LockRetryStrategy is not null)
            {
                return options.LockRetryStrategy;
            }

            if (options.LockRetryStrategyFactory is not null)
            {
                return options.LockRetryStrategyFactory(serviceProvider);
            }

            return new ExponentialBackoffCacheLockRetryStrategy();
        }
    }
}
