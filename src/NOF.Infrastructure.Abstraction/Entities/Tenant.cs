namespace NOF.Infrastructure.Abstraction;

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
