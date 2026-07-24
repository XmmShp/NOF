namespace NOF.Domain;

/// <summary>
/// Abstracts an entity set while preserving LINQ composition and common unit-of-work operations.
/// </summary>
public interface IDbSet<TEntity> : IAsyncQueryable<TEntity>, ICollection<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Adds an entity to the current unit of work.
    /// </summary>
    new void Add(TEntity entity);
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void AddRange(IEnumerable<TEntity> entities);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an entity to the current unit of work.
    /// </summary>
    void Attach(TEntity entity);
    void AttachRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Marks an entity for update.
    /// </summary>
    void Update(TEntity entity);
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Marks an entity for deletion.
    /// </summary>
    new bool Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Returns a non-tracking query for the entity set.
    /// </summary>
    IAsyncQueryable<TEntity> AsNoTracking();
}
