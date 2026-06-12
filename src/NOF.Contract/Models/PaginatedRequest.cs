using System.ComponentModel.DataAnnotations;

namespace NOF.Contract;

/// <summary>
/// Base request for paginated queries.
/// </summary>
public record PaginatedRequest
{
    /// <summary>
    /// The page number (1-based).
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public required int PageNumber { get; init; }

    /// <summary>
    /// The page size.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public required int PageSize { get; init; }
}
