using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

public static partial class NOFInfrastructureExtensions
{
    /// <param name="builder">The <see cref="INOFAppBuilder"/>.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Replaces the default in-memory cache with a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="name">The name of the cache instance.</param>
        /// <param name="connectionName">The name of the connection string in configuration (e.g., "Redis").</param>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(string? name = null, string connectionName = "redis", Action<CacheServiceOptions>? configureOptions = null)
        {
            builder.RemoveRegistrationStep<CacheServiceRegistrationStep>();

            // Register IConnectionMultiplexer if not already registered
            builder.Services.TryAddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(connectionName)
                                       ?? throw new InvalidOperationException($"Connection string '{connectionName}' not found in configuration.");

                return ConnectionMultiplexer.Connect(connectionString);
            });

            builder.Services.AddCacheService<RedisCacheService>(name ?? ICacheServiceFactory.DefaultName, configureOptions);

            return builder;
        }
    }
}
