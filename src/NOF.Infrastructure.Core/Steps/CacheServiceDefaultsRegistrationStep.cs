using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers global default cache dependencies that named cache registrations can fall back to.
/// Runs once through the step pipeline so repeated AddCacheService calls do not duplicate these defaults.
/// </summary>
public sealed class CacheServiceDefaultsRegistrationStep : IBaseSettingsServiceRegistrationStep<CacheServiceDefaultsRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
        builder.Services.TryAddScoped<ICacheService>(sp => sp.GetRequiredKeyedService<ICacheService>(ICacheServiceFactory.DefaultName));
        builder.Services.TryAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
        builder.Services.TryAddSingleton<ICacheServiceFactory, DefaultCacheServiceFactory>();
        return ValueTask.CompletedTask;
    }
}
