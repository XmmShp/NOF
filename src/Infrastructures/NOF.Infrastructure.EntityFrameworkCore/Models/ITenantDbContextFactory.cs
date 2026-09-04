using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Creates strongly typed Entity Framework Core database contexts for explicit tenants.
/// </summary>
/// <typeparam name="TDbContext">The concrete NOF database context type.</typeparam>
public interface ITenantDbContextFactory<TDbContext> : IDbContextFactory<TDbContext>
    where TDbContext : NOFDbContext
{
    /// <summary>
    /// Creates a database context using the connection resolved for <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The tenant whose database context should be created.</param>
    /// <returns>A database context owned by the caller.</returns>
    TDbContext CreateDbContext(string tenantId);
}
