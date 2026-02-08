using System.ComponentModel;

namespace NOF;

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

/// <summary>
/// Tenant entity.
/// </summary>
public class Tenant
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The tenant name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The tenant description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the tenant is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The creation time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The last update time.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
