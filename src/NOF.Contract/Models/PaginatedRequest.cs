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
    public required int PageNumber { get; init; }

    /// <summary>
    /// The page size.
    /// </summary>
    [Required]
    public required int PageSize { get; init; }
}
