using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

public static class NOFInfrastructureExtensions
{
    /// <param name="builder">The <see cref="INOFAppBuilder"/>.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Replaces the default in-memory cache with a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="connectionOptions">StackExchange.Redis connection options.</param>
        /// <param name="configureCacheOptions">Optional action to configure cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(ConfigurationOptions connectionOptions, Action<CacheServiceOptions>? configureCacheOptions = null)
        {
            ArgumentNullException.ThrowIfNull(connectionOptions);

            if (configureCacheOptions is not null)
            {
                builder.Services.Configure(configureCacheOptions);
            }

            builder.Services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionOptions));

            builder.Services.ReplaceOrAddScoped<ICacheServiceRider, RedisCacheServiceRider>();

            return builder;
        }

        /// <summary>
        /// Replaces the default in-memory cache with a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="configuration">The Redis connection string.</param>
        /// <param name="configureConnectionOptions">Configures StackExchange.Redis connection options.</param>
        /// <param name="configureCacheOptions">Optional action to configure cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(string configuration, Action<ConfigurationOptions>? configureConnectionOptions = null, Action<CacheServiceOptions>? configureCacheOptions = null)
        {
            if (configureCacheOptions is not null)
            {
                builder.Services.Configure(configureCacheOptions);
            }

            if (configureConnectionOptions is not null)
            {
                builder.Services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration, configureConnectionOptions));
            }
            else
            {
                builder.Services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration));
            }

            builder.Services.ReplaceOrAddScoped<ICacheServiceRider, RedisCacheServiceRider>();

            return builder;
        }
    }
}
