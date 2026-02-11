using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using System.Reflection;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// NOF database context factory interface
/// Used to create database contexts of the specified type
/// </summary>
public interface INOFDbContextFactory<TDbContext> : IDbContextFactory<TDbContext> where TDbContext : NOFDbContext
{
    /// <summary>
    /// Create a database context for the specified tenant
    /// If tenantId is null or empty, will create Host database context
    /// </summary>
    TDbContext CreateDbContext(string? tenantId);
}

internal sealed class NOFDbContextFactory<TDbContext> : INOFDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInvocationContext _invocationContext;
    private readonly IDbContextConfigurator _dbContextConfigurator;
    private readonly DbContextFactoryOptions _options;
    private readonly ILogger<NOFDbContextFactory<TDbContext>> _logger;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        IInvocationContext invocationContext,
        IDbContextConfigurator dbContextConfigurator,
        IOptions<DbContextFactoryOptions> options,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _invocationContext = invocationContext;
        _dbContextConfigurator = dbContextConfigurator;
        _options = options.Value;
        _logger = logger;
    }

    public TDbContext CreateDbContext()
        => CreateDbContext(_invocationContext.TenantId);

    public TDbContext CreateDbContext(string? tenantId)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        // Always add the tenant extension; TenantId being null/whitespace means host mode
        var extension = new NOFTenantDbContextOptionsExtension { TenantId = tenantId };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        // Use configurator to configure database-specific options
        _dbContextConfigurator.Configure(optionsBuilder, tenantId);

        var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(_serviceProvider, optionsBuilder.Options);

        var contextType = string.IsNullOrWhiteSpace(tenantId) ? "Host" : "Tenant";

        if (Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() != "ef")
        {
            ConfigureDbContext(dbContext, contextType);
        }

        _logger.LogDebug("Created {DbContextType} for {ContextType}", typeof(TDbContext).Name, contextType);
        return dbContext;
    }

    private void ConfigureDbContext(TDbContext dbContext, string contextType)
    {
        if (_options.AutoMigrate)
        {
            if (dbContext.Database.IsRelational())
            {
                dbContext.Database.Migrate();
                _logger.LogDebug("Migrated database for {ContextType}", contextType);
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
                        $"{contextType} database has {pendingMigrations.Length} pending migrations: {string.Join(", ", pendingMigrations)}. " +
                        $"Enable auto-migration by configuring NOFDbContextFactoryOptions.AutoMigrate = true or run migrations manually.");
                }
            }
        }
    }
}
