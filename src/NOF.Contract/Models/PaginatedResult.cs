namespace NOF.Contract;

/// <summary>
/// Paginated result data transfer object.
/// </summary>
/// <typeparam name="T">The type of the paginated items.</typeparam>
public record PaginatedResult<T>
{
    /// <summary>
    /// The current page number.
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// The page size.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// The total number of records.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>
    /// The paginated items.
    /// </summary>
    public required T[] Items { get; init; }
}
