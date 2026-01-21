using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="connectionName">The name of the connection string in configuration (e.g., "Redis").</param>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddRedisCache(string connectionName = "redis",
            Action<CacheServiceOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(connectionName);

            var options = new CacheServiceOptions();
            configureOptions?.Invoke(options);

            // Register IConnectionMultiplexer if not already registered
            services.TryAddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(connectionName)
                                       ?? throw new InvalidOperationException($"Connection string '{connectionName}' not found in configuration.");

                return ConnectionMultiplexer.Connect(connectionString);
            });

            return services.ReplaceOrAddCacheService<RedisCacheService>();
        }
    }
}
