using System.ComponentModel;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Tenant repository interface supporting CRUD operations on tenants.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITenantRepository
{
    /// <summary>
    /// Finds a tenant by ID.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The tenant, or null if not found.</returns>
    ValueTask<Tenant?> FindAsync(string tenantId);

    /// <summary>
    /// Gets all tenants.
    /// </summary>
    /// <returns>A read-only list of all tenants.</returns>
    ValueTask<IReadOnlyList<Tenant>> GetAllAsync();

    /// <summary>
    /// Adds a new tenant.
    /// </summary>
    /// <param name="tenant">The tenant to add.</param>
    void Add(Tenant tenant);

    /// <summary>
    /// Updates a tenant.
    /// </summary>
    /// <param name="tenant">The tenant to update.</param>
    void Update(Tenant tenant);

    /// <summary>
    /// Deletes a tenant by ID.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to delete.</param>
    void Delete(string tenantId);

    /// <summary>
    /// Removes a tenant entity.
    /// </summary>
    /// <param name="tenant">The tenant entity to remove.</param>
    void Remove(Tenant tenant);

    /// <summary>
    /// Checks whether a tenant exists.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>True if the tenant exists; otherwise false.</returns>
    ValueTask<bool> ExistsAsync(string tenantId);
}
