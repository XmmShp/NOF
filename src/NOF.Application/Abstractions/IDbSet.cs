namespace NOF.Application;

/// <summary>
/// Abstracts an entity set while preserving EF Core-style LINQ composition.
/// </summary>
public interface IDbSet<TEntity> : IQueryable<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Adds an entity to the current unit of work.
    /// </summary>
    void Add(TEntity entity);

    /// <summary>
    /// Attaches an entity to the current unit of work.
    /// </summary>
    void Attach(TEntity entity);

    /// <summary>
    /// Marks an entity for update.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Marks an entity for deletion.
    /// </summary>
    void Remove(TEntity entity);

    /// <summary>
    /// Returns a non-tracking query for the entity set.
    /// </summary>
    IQueryable<TEntity> AsNoTracking();
}
