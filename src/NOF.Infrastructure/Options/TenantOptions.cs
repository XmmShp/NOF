using NOF.Abstraction;

namespace NOF.Infrastructure;

/// <summary>
/// Built-in tenant behavior options.
/// </summary>
public sealed class TenantOptions
{
    /// <summary>
    /// Tenant mode of the current application.
    /// Defaults to classic single-tenant mode.
    /// </summary>
    public TenantMode Mode { get; set; } = TenantMode.SingleTenant;

    /// <summary>
    /// Tenant id used in single-tenant mode.
    /// </summary>
    public string SingleTenantId { get; set; } = NOFAbstractionConstants.Tenant.HostId;

    /// <summary>
    /// Database naming format used by DatabasePerTenant mode.
    /// Supports placeholders: {database}, {tenantId}.
    /// </summary>
    public string TenantDatabaseNameFormat { get; set; } = "{database}_{tenantId}";
}

/// <summary>
/// Tenant data isolation mode.
/// </summary>
public enum TenantMode
{
    /// <summary>
    /// Classic single-tenant mode.
    /// Tenant headers are ignored and host tenant is always used.
    /// </summary>
    SingleTenant = 0,

    /// <summary>
    /// Multi-tenant mode with tenant discriminator column (TenantId) in shared database.
    /// </summary>
    SharedDatabase = 1,

    /// <summary>
    /// Multi-tenant mode with one database per tenant in the same DB instance.
    /// </summary>
    DatabasePerTenant = 2
}
