namespace NOF;

/// <summary>
/// Coordinates the persistence of changes across repositories.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Saves all pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}