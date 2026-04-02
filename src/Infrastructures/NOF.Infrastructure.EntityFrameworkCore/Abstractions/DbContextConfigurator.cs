using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Database context configurator interface
/// </summary>
public interface IDbContextConfigurator
{
    /// <summary>
    /// Configure database context options
    /// </summary>
    /// <param name="optionsBuilder">DbContext options builder</param>
    void Configure(DbContextOptionsBuilder optionsBuilder, string tenantId);
}
