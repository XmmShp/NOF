namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers the default in-memory cache service.
/// Can be replaced by adding a different cache implementation (e.g., Redis) after this step.
/// </summary>
public class CacheServiceRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.ReplaceOrAddCacheService<MemoryCacheService>();
        return ValueTask.CompletedTask;
    }
}
