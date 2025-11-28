using MassTransit;
using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Yitter.IdGenerator;

namespace NOF.Test;

/// <summary>
/// 测试基类
/// </summary>
public abstract class UnitTestBase<TDbContext> where TDbContext : DbContext
{
    protected TDbContext DbContext { get; }
    protected IDistributedCache Cache { get; }

    static UnitTestBase()
    {
        YitIdHelper.SetIdGenerator(new IdGeneratorOptions());
    }

    protected UnitTestBase()
    {
        var databaseName = $"{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        var mockMediator = new Mock<IScopedMediator>();

        services.AddSingleton(mockMediator.Object);
        services.AddDbContext<TDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        var serviceProvider = services.BuildServiceProvider().CreateScope().ServiceProvider;
        DbContext = serviceProvider.GetRequiredService<TDbContext>();

        DbContext.Database.EnsureCreated();

        Cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
    }

    protected ILogger<T> GetLogger<T>()
    {
        return new Mock<ILogger<T>>().Object;
    }

    protected ConsumeContext<TMessage> GetConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var mockConsumeContext = new Mock<ConsumeContext<TMessage>>();
        mockConsumeContext.Setup(c => c.Message).Returns(message);
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockConsumeContext.Object;
    }
}