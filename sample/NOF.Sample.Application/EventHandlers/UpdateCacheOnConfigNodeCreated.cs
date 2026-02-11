using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.EventHandlers;

public class UpdateCacheOnConfigNodeCreated : IEventHandler<ConfigNodeCreatedEvent>
{
    private readonly ICacheService _cache;

    public UpdateCacheOnConfigNodeCreated(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(ConfigNodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(@event.Name), cancellationToken);

        var version = DateTime.UtcNow.Ticks;
        await _cache.SetAsync(new ConfigNodeVersionCacheKey(@event.Id),
            version,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            cancellationToken);
    }
}
