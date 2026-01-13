using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace NOF.Test;

/// <summary>
/// 测试基类
/// </summary>
public abstract class UnitTestBase<TDbContext> where TDbContext : DbContext
{
    protected TDbContext DbContext { get; }
    protected ICacheService Cache { get; }

    protected UnitTestBase()
    {
        var databaseName = $"{Guid.NewGuid():N}";
        var services = new ServiceCollection();

        services.AddDbContext<TDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        var serviceProvider = services.BuildServiceProvider().CreateScope().ServiceProvider;
        DbContext = serviceProvider.GetRequiredService<TDbContext>();

        DbContext.Database.EnsureCreated();

        Cache = new MemoryCacheService(new JsonCacheSerializer(), new ExponentialBackoffLockRetryStrategy(), new CacheServiceOptions());
    }

    protected ILogger<T> GetLogger<T>()
    {
        return new Mock<ILogger<T>>().Object;
    }
}