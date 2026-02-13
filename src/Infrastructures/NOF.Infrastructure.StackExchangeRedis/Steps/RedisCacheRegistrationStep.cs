using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Infrastructure.Core;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

/// <summary>
/// Registers a Redis-based cache service using StackExchange.Redis,
/// replacing the default in-memory cache registration.
/// </summary>
public class RedisCacheRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    private readonly string _connectionName;
    private readonly Action<CacheServiceOptions>? _configureOptions;

    public RedisCacheRegistrationStep(string connectionName = "redis",
        Action<CacheServiceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(connectionName);
        _connectionName = connectionName;
        _configureOptions = configureOptions;
    }

    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var options = new CacheServiceOptions();
        _configureOptions?.Invoke(options);

        // Register IConnectionMultiplexer if not already registered
        builder.Services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(_connectionName)
                                   ?? throw new InvalidOperationException($"Connection string '{_connectionName}' not found in configuration.");

            return ConnectionMultiplexer.Connect(connectionString);
        });

        builder.Services.ReplaceOrAddCacheService<RedisCacheService>();

        return ValueTask.CompletedTask;
    }
}
