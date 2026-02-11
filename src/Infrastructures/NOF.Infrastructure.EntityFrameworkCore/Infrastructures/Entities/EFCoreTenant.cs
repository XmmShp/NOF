using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// EF Core tenant entity.
/// </summary>
[HostOnly]
[Table(nameof(EFCoreTenant))]
[Index(nameof(Name), IsUnique = true)]
internal sealed class EFCoreTenant
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    [Key]
    [MaxLength(256)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The tenant name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The tenant description.
    /// </summary>
    [MaxLength(1000)]
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
