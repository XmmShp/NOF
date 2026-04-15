using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using System.Collections.Concurrent;
using System.Reflection;

namespace NOF.Infrastructure;

/// <summary>
/// NOF database context factory interface
/// Used to create database contexts of the specified type
/// </summary>
public interface INOFDbContextFactory<TDbContext> : IDbContextFactory<TDbContext> where TDbContext : NOFDbContext
{
    TDbContext CreateDbContext(string tenantId);
}

internal sealed class NOFDbContextFactory<TDbContext> : INOFDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MigrationLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MigratedContexts = new(StringComparer.Ordinal);

    private readonly IServiceProvider _serviceProvider;
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;
    private readonly IDbContextConfigurator _dbContextConfigurator;
    private readonly DbContextFactoryOptions _options;
    private readonly ILogger<NOFDbContextFactory<TDbContext>> _logger;

    public NOFDbContextFactory(
        IServiceProvider serviceProvider,
        IExecutionContext executionContext,
        IOptions<TenantOptions> tenantOptions,
        IDbContextConfigurator dbContextConfigurator,
        IOptions<DbContextFactoryOptions> options,
        ILogger<NOFDbContextFactory<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
        _dbContextConfigurator = dbContextConfigurator;
        _options = options.Value;
        _logger = logger;
    }

    public TDbContext CreateDbContext()
        => CreateDbContext(_executionContext.TenantId);

    public TDbContext CreateDbContext(string tenantId)
    {
        tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId);
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        var extension = new NOFTenantDbContextOptionsExtension
        {
            TenantId = tenantId,
            TenantMode = _tenantOptions.Mode
        };
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        _dbContextConfigurator.Configure(optionsBuilder, tenantId, _tenantOptions.Mode);
        optionsBuilder.ReplaceService<IModelCustomizer, NOFModelCustomizer>();
        optionsBuilder.ReplaceService<IValueConverterSelector, ValueObjectValueConverterSelector>();
        optionsBuilder.AddInterceptors(ActivatorUtilities.CreateInstance<DomainEventsSaveChangesInterceptor>(_serviceProvider));

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
                EnsureMigratedOnce(dbContext, contextType);
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

    private void EnsureMigratedOnce(TDbContext dbContext, string contextType)
    {
        if (IsSqliteInMemory(dbContext))
        {
            dbContext.Database.EnsureCreated();
            _logger.LogDebug("Initialized in-memory SQLite schema for {ContextType}", contextType);
            return;
        }

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
            var schemaInitialized = false;
            if (hasMigrations)
            {
                dbContext.Database.Migrate();
                schemaInitialized = true;
            }
            else
            {
                schemaInitialized = false;
            }
            MigratedContexts.TryAdd(key, 0);
            if (schemaInitialized)
            {
                _logger.LogDebug("Initialized database schema for {ContextType}", contextType);
            }
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

    private static bool IsSqliteInMemory(TDbContext dbContext)
    {
        var provider = dbContext.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        return connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);
    }
}
