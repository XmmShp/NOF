using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Application.Extension.Redis;
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
        /// <param name="name">The name of the cache instance.</param>
        /// <param name="connectionName">The name of the connection string in configuration (e.g., "Redis").</param>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(string? name = null, string connectionName = "redis", Action<CacheServiceOptions>? configureOptions = null)
        {
            builder.Services.ReplaceOrAddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(connectionName)
                                       ?? throw new InvalidOperationException($"Connection string '{connectionName}' not found in configuration.");

                return ConnectionMultiplexer.Connect(connectionString);
            });

            var cacheName = name ?? ICacheServiceFactory.DefaultName;
            builder.Services.ReplaceOrAddCacheService<RedisCacheService>(cacheName, configureOptions);
            builder.Services.AddKeyedScoped(cacheName, (sp, key) =>
                (IRedisCacheService)sp.GetRequiredKeyedService<ICacheService>(key!));
            builder.Services.ReplaceOrAddScoped(sp =>
                sp.GetRequiredKeyedService<IRedisCacheService>(ICacheServiceFactory.DefaultName));

            return builder;
        }
    }
}
