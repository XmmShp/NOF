using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers the default in-memory cache service under <see cref="ICacheServiceFactory.DefaultName"/>.
/// Can be replaced by adding a different cache implementation (e.g., Redis) after this step.
/// </summary>
public class CacheServiceRegistrationStep : IDependentServiceRegistrationStep<CacheServiceRegistrationStep>, IAfter<CacheServiceDefaultsRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddCacheService<InMemoryCacheService>(ICacheServiceFactory.DefaultName);
        return ValueTask.CompletedTask;
    }
}
