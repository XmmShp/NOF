using NOF.Domain;

namespace NOF.Application;

/// <summary>
/// Coordinates the persistence of changes across repositories.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Marks an aggregate root and its entire object graph as modified.</summary>
    /// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
    /// <param name="entity">The aggregate root to mark as modified.</param>
    void Update<TAggregateRoot>(TAggregateRoot entity) where TAggregateRoot : class, IAggregateRoot;

    /// <summary>Saves all pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
