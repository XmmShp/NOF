using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

public readonly struct EFCoreSelector
{
    public INOFAppBuilder Builder { get; }
    public Type DbContextType { get; }

    public EFCoreSelector(INOFAppBuilder builder, Type dbContextType)
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
