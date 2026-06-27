using Microsoft.Extensions.DependencyInjection;
using NHibernate.Cfg;
using NOF.Hosting;

namespace NOF.Infrastructure.NHibernate;

public readonly struct NHibernateSelector
{
    public INOFAppBuilder Builder { get; }

    public NHibernateSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    public NHibernateSelector WithTenantMode(TenantMode tenantMode)
    {
        Builder.Services.Configure<NHibernateConfigurationOptions>(options =>
        {
            options.TenantMode = tenantMode;
        });
        return this;
    }

    public NHibernateSelector WithConnectionString(string connectionStringTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringTemplate);

        Builder.Services.Configure<NHibernateConfigurationOptions>(options =>
        {
            options.ConnectionStringTemplate = connectionStringTemplate;
        });
        return this;
    }

    public NHibernateSelector WithOptions(Action<Configuration, string> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Builder.Services.Configure<NHibernateConfigurationOptions>(options =>
        {
            options.Configure = configure;
        });
        return this;
    }

    public NHibernateSelector BuildSchemaOnInitialize()
    {
        Builder.Services.Configure<NHibernateConfigurationOptions>(options =>
        {
            options.BuildSchemaOnInitialize = true;
        });
        return this;
    }
}
