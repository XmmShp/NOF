using NHibernate.Cfg;

namespace NOF.Infrastructure.NHibernate;

public sealed class NHibernateConfigurationOptions
{
    public string ConnectionStringTemplate { get; set; } = string.Empty;

    public Action<Configuration, string> Configure { get; set; } = static (_, _) => { };

    public TenantMode TenantMode { get; set; } = TenantMode.DatabasePerTenant;

    public bool BuildSchemaOnInitialize { get; set; }
}

public enum TenantMode
{
    SharedDatabase = 0,
    DatabasePerTenant = 1
}
