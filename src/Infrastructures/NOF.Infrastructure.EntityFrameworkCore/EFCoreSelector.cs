using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

public readonly struct EFCoreSelector
{
    public INOFAppBuilder Builder { get; }

    public EFCoreSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    public EFCoreSelector AutoMigrate()
    {
        Builder.Services.Configure<DbContextFactoryOptions>(options =>
        {
            options.AutoMigrate = true;
        });
        return this;
    }

    public EFCoreSelector UseSingleTenant(string? tenantId = null)
    {
        Builder.Services.Configure<TenantOptions>(options =>
        {
            options.Mode = TenantMode.SingleTenant;
            options.SingleTenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId);
        });
        return this;
    }

    public EFCoreSelector UseSharedDatabaseTenancy()
    {
        Builder.Services.Configure<TenantOptions>(options =>
        {
            options.Mode = TenantMode.SharedDatabase;
        });
        return this;
    }

    public EFCoreSelector UseDatabasePerTenant(string? tenantDatabaseNameFormat = null)
    {
        Builder.Services.Configure<TenantOptions>(options =>
        {
            options.Mode = TenantMode.DatabasePerTenant;
            if (!string.IsNullOrWhiteSpace(tenantDatabaseNameFormat))
            {
                options.TenantDatabaseNameFormat = tenantDatabaseNameFormat;
            }
        });
        return this;
    }
}
