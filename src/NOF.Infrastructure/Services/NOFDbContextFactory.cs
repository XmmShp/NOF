using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

/// <summary>
/// NOF database context factory interface
/// </summary>
public interface INOFDbContextFactory
{
    NOFDbContext CreateDbContext();
    NOFDbContext CreateDbContext(string tenantId);
}

/// <summary>
/// NOF database context factory interface
/// Used to create database contexts of the specified type
/// </summary>
public interface INOFDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext> : INOFDbContextFactory where TDbContext : NOFDbContext
{
    NOFDbContext INOFDbContextFactory.CreateDbContext() => CreateDbContext();
    NOFDbContext INOFDbContextFactory.CreateDbContext(string tenantId) => CreateDbContext(tenantId);

    new TDbContext CreateDbContext();
    new TDbContext CreateDbContext(string tenantId);
}

internal sealed class DbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>(INOFDbContextFactory<TDbContext> dbContextFactory) : IDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    public TDbContext CreateDbContext()
        => dbContextFactory.CreateDbContext();
}

internal sealed class NOFDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext> : INOFDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MigrationLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MigratedContexts = new(StringComparer.Ordinal);

    private readonly IServiceProvider _serviceProvider;
    private readonly IExecutionContext _executionContext;
    private readonly DbContextConfigurationOptions _dbContextConfigurationOptions;
    private readonly ILogger<NOFDbContextFactory<TDbContext>> _logger;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        IExecutionContext executionContext,
        IOptions<DbContextConfigurationOptions> dbContextConfigurationOptions,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _executionContext = executionContext;
        _dbContextConfigurationOptions = dbContextConfigurationOptions.Value;
        _logger = logger;
    }

    public TDbContext CreateDbContext()
        => CreateDbContext(_executionContext.TenantId);

    public TDbContext CreateDbContext(string tenantId)
    {
        tenantId = TenantId.Normalize(tenantId);
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        var extension = new NOFTenantDbContextOptionsExtension
        {
            TenantId = tenantId,
            TenantMode = _dbContextConfigurationOptions.TenantMode
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var connectionString = DbConnectionStringTemplateResolver.ResolveTenantId(_dbContextConfigurationOptions.ConnectionStringTemplate, tenantId);
        _dbContextConfigurationOptions.Configure(optionsBuilder, connectionString);
        optionsBuilder.ReplaceService<IModelCustomizer, NOFModelCustomizer>();
        optionsBuilder.ReplaceService<IValueConverterSelector, ValueObjectValueConverterSelector>();

        var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(_serviceProvider, optionsBuilder.Options);
        EnsureSqliteInMemoryConnectionIsKeptAlive(dbContext);

        var contextType = string.IsNullOrWhiteSpace(tenantId) ? "Host" : "Tenant";

        if (Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() != "ef")
        {
            if (IsSqliteProvider(dbContext))
            {
                EnsureSqliteSchemaInitialized(dbContext, contextType);
            }
        }

        _logger.LogDebug("Created {DbContextType} for {ContextType}", typeof(TDbContext).Name, contextType);
        return dbContext;
    }

    private void EnsureSqliteSchemaInitialized(TDbContext dbContext, string contextType)
    {
        var key = GetMigrationKey(dbContext);
        if (MigratedContexts.ContainsKey(key))
        {
            return;
        }

        var migrationLock = MigrationLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        migrationLock.Wait();
        try
        {
            if (MigratedContexts.ContainsKey(key))
            {
                return;
            }

            var hasMigrations = dbContext.Database.GetMigrations().Any();
            if (hasMigrations)
            {
                dbContext.Database.Migrate();
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }
            MigratedContexts.TryAdd(key, 0);
            _logger.LogDebug("Initialized SQLite schema for {ContextType}", contextType);
        }
        finally
        {
            migrationLock.Release();
        }
    }

    private static string GetMigrationKey(TDbContext dbContext)
    {
        var provider = dbContext.Database.ProviderName ?? "unknown";
        var connectionString = dbContext.Database.GetConnectionString() ?? string.Empty;
        return $"{typeof(TDbContext).AssemblyQualifiedName}|{provider}|{connectionString}";
    }

    private static bool IsSqliteProvider(TDbContext dbContext)
    {
        var provider = dbContext.Database.ProviderName ?? string.Empty;
        return provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureSqliteInMemoryConnectionIsKeptAlive(TDbContext dbContext)
    {
        if (!IsSqliteProvider(dbContext))
        {
            return;
        }

        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (!connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var keeper = _serviceProvider.GetService<SqliteInMemoryConnectionKeeper>();
        keeper?.EnsureConnection(connectionString);
    }
}
