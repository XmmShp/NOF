using Microsoft.EntityFrameworkCore;

namespace NOF;

/// <summary>
/// Database context configurator interface
/// </summary>
public interface IDbContextConfigurator
{
    /// <summary>
    /// Configure database context options
    /// </summary>
    /// <param name="optionsBuilder">DbContext options builder</param>
    /// <param name="tenantId">Tenant ID, can be null for Host environment</param>
    void Configure(DbContextOptionsBuilder optionsBuilder, string? tenantId);
}
