using NOF.Application;

namespace System.Linq;

/// <summary>
/// Expression-based query projection helpers.
/// </summary>
public static class MappingQueryableExtensions
{
    extension(IQueryable source)
    {
        /// <summary>
        /// Applies the ambient mapper's expression as the query's projection.
        /// Filtering, ordering, and paging should be composed before this call.
        /// </summary>
        public IQueryable<TDestination> ProjectTo<TDestination>(
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            return Mapper.Current.ProjectTo<TDestination>(source, name);
        }

        /// <summary>
        /// Applies the provided mapper's expression as the query's projection.
        /// </summary>
        public IQueryable<TDestination> ProjectTo<TDestination>(
            IMapper mapper,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(mapper);

            return mapper.ProjectTo<TDestination>(source, name);
        }
    }
}
