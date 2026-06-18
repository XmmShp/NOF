namespace NOF.Application;

/// <summary>
/// Abstracts the application's persistence context.
/// Implementations may be backed by any concrete data access technology.
/// </summary>
public interface IDbContext
{
    /// <summary>
    /// Gets a query/update entry point for <typeparamref name="TEntity"/>.
    /// </summary>
    IDbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    /// <summary>
    /// Persists pending changes.
    /// </summary>
    int SaveChanges();

    /// <summary>
    /// Persists pending changes.
    /// </summary>
    int SaveChanges(bool acceptAllChangesOnSuccess);

    /// <summary>
    /// Persists pending changes asynchronously.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending changes asynchronously.
    /// </summary>
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
}
