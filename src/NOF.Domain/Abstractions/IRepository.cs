namespace NOF.Domain;

/// <summary>
/// Generic repository interface for aggregate root persistence and querying.
/// </summary>
/// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
public interface IRepository<TAggregateRoot> : IQueryable<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>
    /// Gets a no-tracking queryable source for read-only scenarios.
    /// </summary>
    IQueryable<TAggregateRoot> AsNoTracking();

    /// <summary>Finds an aggregate root by its composite key values.</summary>
    /// <param name="keyValues">The primary key values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>Returns all aggregate roots from the repository as an async stream.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of all aggregate roots.</returns>
    IAsyncEnumerable<TAggregateRoot> FindAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a parameterized SQL query for the aggregate root set.
    /// </summary>
    /// <param name="sql">The interpolated SQL query.</param>
    /// <returns>A queryable for the SQL query.</returns>
    IQueryable<TAggregateRoot> FromSql(FormattableString sql);

    /// <summary>
    /// Creates a raw SQL query for the aggregate root set.
    /// </summary>
    /// <param name="sql">The raw SQL query.</param>
    /// <param name="parameters">The SQL parameters.</param>
    /// <returns>A queryable for the SQL query.</returns>
    IQueryable<TAggregateRoot> FromSqlRaw(string sql, params object?[] parameters);

    /// <summary>
    /// Executes a parameterized SQL command.
    /// </summary>
    /// <param name="sql">The interpolated SQL command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw SQL command.
    /// </summary>
    /// <param name="sql">The raw SQL command.</param>
    /// <param name="parameters">The SQL parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> ExecuteSqlRawAsync(string sql, object?[]? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Adds an aggregate root to the repository.</summary>
    /// <param name="entity">The aggregate root to add.</param>
    void Add(TAggregateRoot entity);

    /// <summary>Removes an aggregate root from the repository.</summary>
    /// <param name="entity">The aggregate root to remove.</param>
    void Remove(TAggregateRoot entity);
}

/// <summary>
/// Repository interface for aggregate roots with a single-value primary key.
/// </summary>
/// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The primary key type.</typeparam>
public interface IRepository<TAggregateRoot, in TKey> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>Finds an aggregate root by its primary key.</summary>
    /// <param name="key">The primary key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(TKey key, CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key], cancellationToken: cancellationToken);
}

/// <summary>
/// Repository interface for aggregate roots with a composite key of two values.
/// </summary>
/// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
/// <typeparam name="TKey1">The first key type.</typeparam>
/// <typeparam name="TKey2">The second key type.</typeparam>
public interface IRepository<TAggregateRoot, in TKey1, in TKey2> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>Finds an aggregate root by its composite key.</summary>
    /// <param name="key1">The first key value.</param>
    /// <param name="key2">The second key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(TKey1 key1, TKey2 key2, CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key1, key2], cancellationToken: cancellationToken);
}

/// <summary>
/// Repository interface for aggregate roots with a composite key of three values.
/// </summary>
/// <typeparam name="TAggregateRoot">The aggregate root type.</typeparam>
/// <typeparam name="TKey1">The first key type.</typeparam>
/// <typeparam name="TKey2">The second key type.</typeparam>
/// <typeparam name="TKey3">The third key type.</typeparam>
public interface IRepository<TAggregateRoot, in TKey1, in TKey2, in TKey3> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>Finds an aggregate root by its composite key.</summary>
    /// <param name="key1">The first key value.</param>
    /// <param name="key2">The second key value.</param>
    /// <param name="key3">The third key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate root, or <c>null</c> if not found.</returns>
    ValueTask<TAggregateRoot?> FindAsync(TKey1 key1, TKey2 key2, TKey3 key3, CancellationToken cancellationToken = default)
        => FindAsync(keyValues: [key1, key2, key3], cancellationToken: cancellationToken);
}
