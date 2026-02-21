using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers the default in-memory cache service under <see cref="ICacheServiceFactory.DefaultName"/>.
/// Can be replaced by adding a different cache implementation (e.g., Redis) after this step.
/// </summary>
public class CacheServiceRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddCacheService<MemoryCacheService>(ICacheServiceFactory.DefaultName);
        return ValueTask.CompletedTask;
    }
}
