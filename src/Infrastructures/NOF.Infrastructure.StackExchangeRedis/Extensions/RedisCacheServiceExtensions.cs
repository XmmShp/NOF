using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.StackExchangeRedis;

public static partial class NOFInfrastructureExtensions
{
    /// <param name="builder">The <see cref="INOFAppBuilder"/>.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Replaces the default in-memory cache with a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="connectionName">The name of the connection string in configuration (e.g., "Redis").</param>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(string connectionName = "redis",
            Action<CacheServiceOptions>? configureOptions = null)
        {
            builder.RemoveRegistrationStep<CacheServiceRegistrationStep>();
            builder.AddRegistrationStep(new RedisCacheRegistrationStep(connectionName, configureOptions));
            return builder;
        }
    }
}
