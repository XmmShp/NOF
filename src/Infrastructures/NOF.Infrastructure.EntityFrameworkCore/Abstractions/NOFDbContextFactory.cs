using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NOF;

/// <summary>
/// NOF 数据库上下文工厂接口
/// 用于创建指定类型的数据库上下文
/// </summary>
public interface INOFDbContextFactory
{
    /// <summary>
    /// 创建指定租户的数据库上下文
    /// </summary>
    DbContextBundle<TDbContext> GetDbContextBundle<TDbContext>(string tenantId) where TDbContext : NOFDbContext;
}

public sealed class DbContextBundle<TDbContext> where TDbContext : NOFDbContext
{
    public IUnitOfWork UnitOfWork { get; init; }
    public ITransactionManager TransactionManager { get; init; }
    public TDbContext Repository { get; init; }
}

internal sealed class NOFDbContextFactory : INOFDbContextFactory, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IStartupEventChannel _startupEventChannel;
    private readonly bool _autoMigrate;
    private readonly ILogger<NOFDbContextFactory> _logger;
    private readonly Dictionary<(Type DbContextType, string TenantId), object> _cache = new();
    private bool _disposed;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        IStartupEventChannel startupEventChannel,
        bool autoMigrate,
        ILogger<NOFDbContextFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _startupEventChannel = startupEventChannel;
        _autoMigrate = autoMigrate;
        _logger = logger;
    }

    public DbContextBundle<TDbContext> GetDbContextBundle<TDbContext>(string tenantId) where TDbContext : NOFDbContext
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        var key = (typeof(TDbContext), tenantId);

        if (_cache.TryGetValue(key, out var cached))
        {
            return (DbContextBundle<TDbContext>)cached;
        }

        var dbContext = CreateTenantDbContext<TDbContext>(tenantId);
        _cache[key] = dbContext;
        return dbContext;
    }

    private DbContextBundle<TDbContext> CreateTenantDbContext<TDbContext>(string tenantId) where TDbContext : NOFDbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        var extension = new NOFDbContextOptionsExtension(_startupEventChannel);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var configurating = new DbContextConfigurating(_serviceProvider, tenantId, optionsBuilder);
        _startupEventChannel.Publish(configurating);
        var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(_serviceProvider, optionsBuilder.Options);

        ConfigureTenantDbContext(dbContext, tenantId);
        var unitOfWork = ActivatorUtilities.CreateInstance<EFCoreUnitOfWork>(_serviceProvider, dbContext);
        var transactionManager = ActivatorUtilities.CreateInstance<EFCoreTransactionManager>(_serviceProvider, dbContext);
        var dbContextBundle = new DbContextBundle<TDbContext>
        {
            UnitOfWork = unitOfWork,
            TransactionManager = transactionManager,
            Repository = dbContext
        };

        _logger.LogDebug("Created {DbContextType} for tenant {TenantId}", typeof(TDbContext).Name, tenantId);
        return dbContextBundle;
    }

    private void ConfigureTenantDbContext<TDbContext>(TDbContext dbContext, string tenantId) where TDbContext : NOFDbContext
    {
        if (Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() != "ef")
        {
            if (_autoMigrate)
            {
                if (dbContext.Database.IsRelational())
                {
                    dbContext.Database.Migrate();
                    _logger.LogDebug("Migrated database for tenant {TenantId}", tenantId);
                }
            }
            else
            {
                if (dbContext.Database.IsRelational())
                {
                    var pendingMigrations = dbContext.Database.GetPendingMigrations().ToArray();
                    if (pendingMigrations.Length != 0)
                    {
                        throw new InvalidOperationException(
                            $"Tenant {tenantId} database has {pendingMigrations.Length} pending migrations: {string.Join(", ", pendingMigrations)}. " +
                            $"Enable auto-migration by setting builder.AutoMigrateTenantDatabases = true or run migrations manually.");
                    }
                }
            }
        }

    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var dbContext in _cache.Values)
            {
                if (dbContext is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _cache.Clear();
            _disposed = true;
        }
    }
}
