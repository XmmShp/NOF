using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// 数据访问上下文工厂实现
/// 基于现有的 INOFDbContextFactory 创建不同类型的数据访问上下文
/// </summary>
public class EFCoreDataAccessContextFactory<TDbContext> :
    IDataAccessContextFactory,
    IPublicDataAccessContextFactory
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantContext _tenantContext;

    public EFCoreDataAccessContextFactory(IServiceProvider serviceProvider, ITenantContext tenantContext)
    {
        _serviceProvider = serviceProvider;
        _tenantContext = tenantContext;
    }

    public IDataAccessContext CreateTenantContext()
    {
        return CreateTenantContext(_tenantContext.CurrentTenantId);
    }

    public IDataAccessContext CreateTenantContext(string tenantId)
    {
        var tenantDbContextFactory = _serviceProvider.GetRequiredService<INOFDbContextFactory<TDbContext>>();
        var tenantDbContext = tenantDbContextFactory.CreateDbContext(tenantId);

        return new EFCoreDataAccessContext<TDbContext>(tenantDbContext, _serviceProvider);
    }

    public IDataAccessContext CreatePublicContext()
    {
        var publicDbContext = _serviceProvider.GetRequiredService<TDbContext>();
        return new EFCoreDataAccessContext<TDbContext>(publicDbContext, _serviceProvider);
    }
}
