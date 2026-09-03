using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Configures tenant-aware Entity Framework Core database context creation.
/// </summary>
public sealed class DbContextConfigurationOptions
{
    /// <summary>
    /// Gets or sets the fallback connection-string template. The default resolver replaces
    /// <c>{tenantId}</c> with the normalized tenant identifier.
    /// </summary>
    public string ConnectionStringTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection-string resolver invoked for every database context creation.
    /// </summary>
    public DbContextConnectionStringResolver ConnectionStringResolver { get; set; } = static context =>
        DbConnectionStringTemplateResolver.ResolveTenantId(context.ConnectionStringTemplate, context.TenantId);

    /// <summary>
    /// Gets or sets the provider-specific database context options configuration.
    /// </summary>
    public Action<DbContextOptionsBuilder, string> Configure { get; set; } = static (_, _) => { };

    /// <summary>
    /// Gets or sets the tenant storage mode.
    /// </summary>
    public TenantMode TenantMode { get; set; } = TenantMode.DatabasePerTenant;

    /// <summary>
    /// Gets or sets whether soft delete is enabled by default.
    /// </summary>
    public bool SoftDeleteEnabled { get; set; } = true;
}

/// <summary>
/// Defines how tenant data is separated by the persistence provider.
/// </summary>
public enum TenantMode
{
    /// <summary>
    /// Stores tenants in one database and applies tenant-aware model filters.
    /// </summary>
    SharedDatabase = 0,

    /// <summary>
    /// Resolves a database connection independently for each tenant.
    /// </summary>
    DatabasePerTenant = 1
}
