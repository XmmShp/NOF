namespace NOF.Infrastructure;

/// <summary>
/// Tenant aggregate root.
/// </summary>
public class NOFTenant
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public required TenantId Id { get; set; }

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
