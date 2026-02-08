using System.ComponentModel.DataAnnotations;

namespace NOF;

/// <summary>
/// Base request for paginated queries.
/// </summary>
public record PaginatedRequest<T> : IRequest<T>
    where T : class, IPaginatedResult
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