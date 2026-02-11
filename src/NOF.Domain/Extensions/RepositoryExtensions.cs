namespace NOF;

/// <summary>
/// Extension methods for the NOF.Domain layer.
/// </summary>
public static partial class NOFDomainExtensions
{
    extension<TAggregateRoot>(IRepository<TAggregateRoot> repository)
        where TAggregateRoot : class, IAggregateRoot
    {
        /// <summary>Finds an aggregate root by a single key value.</summary>
        /// <param name="key">The primary key value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
        public ValueTask<TAggregateRoot?> FindAsync(
            object? key,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key], cancellationToken: cancellationToken);

        /// <summary>Finds an aggregate root by a composite key of two values.</summary>
        /// <param name="key1">The first key value.</param>
        /// <param name="key2">The second key value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
        public ValueTask<TAggregateRoot?> FindAsync(
            object? key1,
            object? key2,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key1, key2], cancellationToken: cancellationToken);

        /// <summary>Finds an aggregate root by a composite key of three values.</summary>
        /// <param name="key1">The first key value.</param>
        /// <param name="key2">The second key value.</param>
        /// <param name="key3">The third key value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
        public ValueTask<TAggregateRoot?> FindAsync(
            object? key1,
            object? key2,
            object? key3,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key1, key2, key3], cancellationToken: cancellationToken);
    }
}
