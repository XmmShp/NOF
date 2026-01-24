using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// EFCore repository factory implementation
/// </summary>
internal sealed class EFCoreRepositoryFactory<TDbContext> : IRepositoryFactory
    where TDbContext : NOFDbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INOFDbContextFactory _dbContextFactory;

    public EFCoreRepositoryFactory(IServiceProvider serviceProvider, INOFDbContextFactory dbContextFactory)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
    }

    public RepositoryBundle<TRepository> GetRepositoryBundle<TRepository>(string tenantId)
        where TRepository : IRepository
    {
        var dbContextBundle = _dbContextFactory.GetDbContextBundle<TDbContext>(tenantId);
        var repository = ActivatorUtilities.CreateInstance<TRepository>(_serviceProvider, dbContextBundle.Repository);

        return new RepositoryBundle<TRepository>
        {
            UnitOfWork = dbContextBundle.UnitOfWork,
            TransactionManager = dbContextBundle.TransactionManager,
            Repository = repository
        };
    }
}
