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
public interface INOFDbContextFactory<TDbContext> : IDbContextFactory<TDbContext> where TDbContext : DbContext
{
    /// <summary>
    /// 创建指定租户的数据库上下文
    /// </summary>
    TDbContext CreateDbContext(string tenantId);
}

internal sealed class NOFDbContextFactory<TDbContext> : INOFDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IStartupEventChannel _startupEventChannel;
    private readonly bool _autoMigrate;
    private readonly ILogger<NOFDbContextFactory<TDbContext>> _logger;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        ITenantContext tenantContext,
        IStartupEventChannel startupEventChannel,
        bool autoMigrate,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _tenantContext = tenantContext;
        _startupEventChannel = startupEventChannel;
        _autoMigrate = autoMigrate;
        _logger = logger;
    }

    public TDbContext CreateDbContext()
        => CreateDbContext(_tenantContext.CurrentTenantId);

    public TDbContext CreateDbContext(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        var extension = new NOFDbContextOptionsExtension(_startupEventChannel);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var configurating = new DbContextConfigurating(_serviceProvider, tenantId, optionsBuilder);
        _startupEventChannel.Publish(configurating);
        var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(_serviceProvider, optionsBuilder.Options);

        ConfigureDbContext(dbContext, tenantId);

        _logger.LogDebug("Created {DbContextType} for tenant {TenantId}", typeof(TDbContext).Name, tenantId);
        return dbContext;
    }

    private void ConfigureDbContext(TDbContext dbContext, string tenantId)
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
}
