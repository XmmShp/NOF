using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure;

public sealed class DbContextConfigurationOptions
{
    public string ConnectionStringTemplate { get; set; } = string.Empty;

    public Action<DbContextOptionsBuilder, string> Configure { get; set; } = static (_, _) => { };

    public TenantMode TenantMode { get; set; } = TenantMode.DatabasePerTenant;
}

public enum TenantMode
{
    SharedDatabase = 0,
    DatabasePerTenant = 1
}
