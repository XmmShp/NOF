using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NHibernate.Cfg;

namespace NOF.Infrastructure.NHibernate;

public readonly struct NHibernateSelector
{
    public IHostApplicationBuilder Builder { get; }

    public NHibernateSelector(IHostApplicationBuilder builder)
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
