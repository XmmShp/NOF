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

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class TypedDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>(NOFDbContextFactory<TDbContext> dbContextFactory) : IDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    public TDbContext CreateDbContext()
        => dbContextFactory.CreateConcreteDbContext();
}

internal sealed class NOFDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext> : IDbContextFactory
    where TDbContext : NOFDbContext
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MigrationLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MigratedContexts = new(StringComparer.Ordinal);

    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly DbContextConfigurationOptions _dbContextConfigurationOptions;
    private readonly IEnumerable<IDbContextModelCreatingContributor> _modelCreatingContributors;
    private readonly ILogger<NOFDbContextFactory<TDbContext>> _logger;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        ICurrentTenant currentTenant,
        IOptions<DbContextConfigurationOptions> dbContextConfigurationOptions,
        IEnumerable<IDbContextModelCreatingContributor> modelCreatingContributors,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _currentTenant = currentTenant;
        _dbContextConfigurationOptions = dbContextConfigurationOptions.Value;
        _modelCreatingContributors = modelCreatingContributors;
        _logger = logger;
    }

    public IDbContext CreateDbContext()
        => new EfCoreDbContextAdapter(CreateConcreteDbContext());

    public IDbContext CreateDbContext(string tenantId)
        => new EfCoreDbContextAdapter(CreateConcreteDbContext(tenantId));

    internal TDbContext CreateConcreteDbContext()
        => CreateConcreteDbContext(TenantId.Normalize(_currentTenant.TenantId));

    internal TDbContext CreateConcreteDbContext(string tenantId)
    {
        tenantId = TenantId.Normalize(tenantId);
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        var extension = new NOFTenantDbContextOptionsExtension
        {
            TenantId = tenantId,
            TenantMode = _dbContextConfigurationOptions.TenantMode,
            SoftDeleteEnabled = _dbContextConfigurationOptions.SoftDeleteEnabled
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var modelCreatingExtension = new NOFModelCreatingDbContextOptionsExtension
        {
            Contributors = [.. _modelCreatingContributors]
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(modelCreatingExtension);

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
