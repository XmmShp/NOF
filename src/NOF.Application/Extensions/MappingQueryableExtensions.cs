using NOF.Application;

namespace System.Linq;

/// <summary>
/// Expression-based query projection helpers.
/// </summary>
public static class MappingQueryableExtensions
{
    /// <summary>
    /// Applies the ambient mapper's expression as the query's projection.
    /// Filtering, ordering, and paging should be composed before this call.
    /// </summary>
    public static IQueryable<TDestination> ProjectTo<TDestination>(
        this IQueryable source,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Mapper.Current.ProjectTo<TDestination>(source, name);
    }

    /// <summary>
    /// Applies the provided mapper's expression as the query's projection.
    /// </summary>
    public static IQueryable<TDestination> ProjectTo<TDestination>(
        this IQueryable source,
        IMapper mapper,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        return mapper.ProjectTo<TDestination>(source, name);
    }
}
