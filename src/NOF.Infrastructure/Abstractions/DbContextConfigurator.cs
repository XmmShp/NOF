using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure;

/// <summary>
/// Database context configurator interface
/// </summary>
public interface IDbContextConfigurator
{
    /// <summary>
    /// Configure database context options
    /// </summary>
    /// <param name="optionsBuilder">DbContext options builder</param>
    /// <param name="tenantMode">Tenant mode.</param>
    void Configure(DbContextOptionsBuilder optionsBuilder, string tenantId, TenantMode tenantMode);
}
