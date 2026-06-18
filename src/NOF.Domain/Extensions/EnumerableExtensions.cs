namespace System.Collections.Generic;

/// <summary>
/// Provides extension members for collection types.
/// </summary>
public static class EnumerableExtensions
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
