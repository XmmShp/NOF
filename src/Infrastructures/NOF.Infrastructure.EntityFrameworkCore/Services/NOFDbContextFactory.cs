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

/// <summary>
/// Creates tenant-aware Entity Framework Core database contexts for NOF persistence.
/// </summary>
/// <typeparam name="TDbContext">The concrete NOF database context type.</typeparam>
public class NOFDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>
    : ITenantDbContextFactory<TDbContext>, IDbContextFactory
    where TDbContext : NOFDbContext
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MigrationLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MigratedContexts = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a derived tenant-aware database context factory from its scoped service provider.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider used to resolve factory dependencies.</param>
    protected NOFDbContextFactory(IServiceProvider serviceProvider)
        : this(
            serviceProvider,
            serviceProvider.GetRequiredService<ICurrentTenant>(),
            serviceProvider.GetRequiredService<IOptions<DbContextConfigurationOptions>>(),
            serviceProvider.GetServices<IDbContextModelCreatingContributor>(),
            serviceProvider.GetRequiredService<ILogger<NOFDbContextFactory<TDbContext>>>())
    {
    }

    /// <summary>
    /// Initializes a tenant-aware database context factory.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider used to construct contexts.</param>
    /// <param name="currentTenant">The current tenant accessor.</param>
    /// <param name="dbContextConfigurationOptions">The configured database context options.</param>
    /// <param name="modelCreatingContributors">The registered model contributors.</param>
    /// <param name="logger">The factory logger.</param>
    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        ICurrentTenant currentTenant,
        IOptions<DbContextConfigurationOptions> dbContextConfigurationOptions,
        IEnumerable<IDbContextModelCreatingContributor> modelCreatingContributors,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        ServiceProvider = serviceProvider;
        CurrentTenant = currentTenant;
        ConfigurationOptions = dbContextConfigurationOptions.Value;
        ModelCreatingContributors = modelCreatingContributors;
        Logger = logger;
    }

    /// <summary>
    /// Gets the scoped service provider used to construct contexts.
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the current tenant accessor.
    /// </summary>
    protected ICurrentTenant CurrentTenant { get; }

    /// <summary>
    /// Gets the configured database context options.
    /// </summary>
    protected DbContextConfigurationOptions ConfigurationOptions { get; }

    /// <summary>
    /// Gets the model contributors applied to created contexts.
    /// </summary>
    protected IEnumerable<IDbContextModelCreatingContributor> ModelCreatingContributors { get; }

    /// <summary>
    /// Gets the factory logger.
    /// </summary>
    protected ILogger<NOFDbContextFactory<TDbContext>> Logger { get; }

    /// <summary>
    /// Creates a strongly typed database context for the current tenant.
    /// </summary>
    /// <returns>A database context owned by the caller.</returns>
    public virtual TDbContext CreateDbContext()
        => CreateDbContext(TenantId.Normalize(CurrentTenant.TenantId));

    /// <summary>
    /// Creates a strongly typed database context for an explicit tenant.
    /// </summary>
    /// <param name="tenantId">The tenant whose connection should be resolved.</param>
    /// <returns>A database context owned by the caller.</returns>
    public virtual TDbContext CreateDbContext(string tenantId)
    {
        tenantId = TenantId.Normalize(tenantId);
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        var extension = new NOFTenantDbContextOptionsExtension
        {
            TenantId = tenantId,
            TenantMode = ConfigurationOptions.TenantMode,
            SoftDeleteEnabled = ConfigurationOptions.SoftDeleteEnabled
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var modelCreatingExtension = new NOFModelCreatingDbContextOptionsExtension
        {
            Contributors = [.. ModelCreatingContributors]
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(modelCreatingExtension);

        var resolutionContext = new DbContextConnectionStringResolutionContext(
            ServiceProvider,
            typeof(TDbContext),
            tenantId,
            ConfigurationOptions.TenantMode,
            ConfigurationOptions.ConnectionStringTemplate);
        var resolver = ConfigurationOptions.ConnectionStringResolver
            ?? throw new InvalidOperationException("The database context connection-string resolver is not configured.");
        var connectionString = resolver(resolutionContext)
            ?? throw new InvalidOperationException(
                $"The connection-string resolver returned null for DbContext '{typeof(TDbContext).FullName}' and tenant '{tenantId}'.");
        ConfigurationOptions.Configure(optionsBuilder, connectionString);
        optionsBuilder.ReplaceService<IModelCustomizer, NOFModelCustomizer>();
        optionsBuilder.ReplaceService<IValueConverterSelector, ValueObjectValueConverterSelector>();

        var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(ServiceProvider, optionsBuilder.Options);
        EnsureSqliteInMemoryConnectionIsKeptAlive(dbContext);

        var contextType = string.IsNullOrWhiteSpace(tenantId) ? "Host" : "Tenant";

        if (Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() != "ef")
        {
            if (IsSqliteProvider(dbContext))
            {
                EnsureSqliteSchemaInitialized(dbContext, contextType);
            }
        }

        Logger.LogDebug("Created {DbContextType} for {ContextType}", typeof(TDbContext).Name, contextType);
        return dbContext;
    }

    IDbContext IDbContextFactory.CreateDbContext()
        => new EfCoreDbContextAdapter(CreateDbContext());

    IDbContext IDbContextFactory.CreateDbContext(string tenantId)
        => new EfCoreDbContextAdapter(CreateDbContext(tenantId));

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
            Logger.LogDebug("Initialized SQLite schema for {ContextType}", contextType);
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

        var keeper = ServiceProvider.GetService<SqliteInMemoryConnectionKeeper>();
        keeper?.EnsureConnection(connectionString);
    }
}
