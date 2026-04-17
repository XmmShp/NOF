using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public readonly struct EFCoreSelector
{
    public INOFAppBuilder Builder { get; }

    public EFCoreSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    public EFCoreSelector WithTenantMode(TenantMode tenantMode)
    {
        Builder.Services.Configure<DbContextConfigurationOptions>(options =>
        {
            options.TenantMode = tenantMode;
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
}
