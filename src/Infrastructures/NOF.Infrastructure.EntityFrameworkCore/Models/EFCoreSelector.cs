using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

public readonly struct EFCoreSelector
{
    public IHostApplicationBuilder Builder { get; }
    public Type DbContextType { get; }

    public EFCoreSelector(IHostApplicationBuilder builder, Type dbContextType)
    {
        Builder = builder;
        DbContextType = dbContextType;
    }

    public EFCoreSelector WithTenantMode(TenantMode tenantMode)
    {
        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.TenantMode = tenantMode;
        });
        return this;
    }

    public EFCoreSelector WithSoftDelete(bool enabled)
    {
        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.SoftDeleteEnabled = enabled;
        });
        return this;
    }

    public EFCoreSelector WithConnectionString(string connectionStringTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringTemplate);

        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.ConnectionStringTemplate = connectionStringTemplate;
        });
        return this;
    }

    /// <summary>
    /// Configures a connection-string resolver that runs in the current database context scope.
    /// The resolver can use the tenant, database context type, tenant mode, fallback template,
    /// and scoped services.
    /// </summary>
    /// <param name="resolver">The connection-string resolver.</param>
    /// <returns>The current selector.</returns>
    public EFCoreSelector WithConnectionStringResolver(DbContextConnectionStringResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.ConnectionStringResolver = resolver;
        });
        return this;
    }

    public EFCoreSelector WithOptions(Action<DbContextOptionsBuilder, string> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.Configure = configure;
        });
        return this;
    }

    public EFCoreSelector MigrateOnInitialize()
    {
        var dbContextType = DbContextType;
        Builder.Services.RemoveInitializationStep<DbContextMigrationInitializationStep>(existing =>
            existing.DbContextType == dbContextType);
        Builder.Services.AddInitializationStep(new DbContextMigrationInitializationStep(dbContextType));
        return this;
    }
}
