using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NOF;

internal class EFCoreDataAccessContext<TDbContext> : IDataAccessContext
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, object> _fieldCache = new();

    public EFCoreDataAccessContext(TDbContext dbContext, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
    }

    public IUnitOfWork UnitOfWork => field ??= new EFCoreUnitOfWork(
        _dbContext,
        TransactionManager,
        _serviceProvider.GetRequiredService<IEventPublisher>(),
        _serviceProvider.GetRequiredService<IOutboxMessageRepository>(),
        _serviceProvider.GetRequiredService<IOutboxMessageCollector>());

    public ITransactionManager TransactionManager => field ??= new EFCoreTransactionManager(
        _dbContext,
        _serviceProvider.GetRequiredService<ILogger<EFCoreTransactionManager>>());

    public TRepository GetRepository<TRepository>()
        where TRepository : class
    {
        return (TRepository)_fieldCache.GetOrAdd(typeof(TRepository), type =>
        {
            var interceptingProvider = new DbContextInterceptingServiceProvider<TDbContext>(_serviceProvider, _dbContext);

            return interceptingProvider.GetRequiredService<TRepository>();
        });
    }
}

/// <summary>
/// DbContext 拦截器服务提供者，用于将所有 DbContext 相关的依赖解析重定向到指定的 DbContext 实例
/// </summary>
internal sealed class DbContextInterceptingServiceProvider<TDbContext> : IServiceProvider
    where TDbContext : DbContext
{
    private readonly IServiceProvider _innerProvider;
    private readonly TDbContext _targetDbContext;

    public DbContextInterceptingServiceProvider(IServiceProvider innerProvider, TDbContext targetDbContext)
    {
        _innerProvider = innerProvider;
        _targetDbContext = targetDbContext;
    }

    public object? GetService(Type serviceType)
    {
        if (ShouldIntercept(serviceType))
        {
            return _targetDbContext;
        }

        return _innerProvider.GetService(serviceType);
    }

    private static bool ShouldIntercept(Type serviceType)
    {
        return serviceType.IsAssignableFrom(typeof(TDbContext));
    }
}
