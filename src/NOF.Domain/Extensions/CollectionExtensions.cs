namespace NOF.Domain;

/// <summary>
/// Provides extension members for collection types.
/// </summary>
public static class CollectionExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Casts the enumerable to <see cref="ICollection{T}"/> for mutation.
        /// The underlying collection must implement <see cref="ICollection{T}"/>.
        /// </summary>
        public ICollection<T> Mut => (ICollection<T>)source;
    }
}
