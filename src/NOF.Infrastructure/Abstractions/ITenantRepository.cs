using NOF.Domain;
using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Repository interface for tenant aggregate roots.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITenantRepository : IRepository<NOFTenant, string>
{
    /// <summary>
    /// Checks whether a tenant exists.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>True if the tenant exists; otherwise false.</returns>
    ValueTask<bool> ExistsAsync(string tenantId);
}
