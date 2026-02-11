using System.ComponentModel;

namespace NOF;

/// <summary>
/// Non-generic marker interface for repositories. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRepository;

/// <summary>
/// Generic repository interface for aggregate root persistence.
/// </summary>
/// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
public interface IRepository<TAggregateRoot> : IRepository
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>Finds an aggregate root by its composite key values.</summary>
    /// <param name="keyValues">The primary key values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default);
    /// <summary>Adds an aggregate root to the repository.</summary>
    /// <param name="entity">The aggregate root to add.</param>
    void Add(TAggregateRoot entity);
    /// <summary>Removes an aggregate root from the repository.</summary>
    /// <param name="entity">The aggregate root to remove.</param>
    void Remove(TAggregateRoot entity);

    /// <summary>Finds an aggregate root by a single key value.</summary>
    /// <param name="key">The primary key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(
        object? key,
        CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key], cancellationToken: cancellationToken);

    /// <summary>Finds an aggregate root by a composite key of two values.</summary>
    /// <param name="key1">The first key value.</param>
    /// <param name="key2">The second key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(
        object? key1,
        object? key2,
        CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key1, key2], cancellationToken: cancellationToken);

    /// <summary>Finds an aggregate root by a composite key of three values.</summary>
    /// <param name="key1">The first key value.</param>
    /// <param name="key2">The second key value.</param>
    /// <param name="key3">The third key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(
        object? key1,
        object? key2,
        object? key3,
        CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key1, key2, key3], cancellationToken: cancellationToken);
}
