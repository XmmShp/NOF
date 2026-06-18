namespace NOF.Application;

/// <summary>
/// Represents a database transaction managed through NOF persistence abstractions.
/// </summary>
public interface IDbContextTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the transaction identifier.
    /// </summary>
    Guid TransactionId { get; }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    void Commit();

    /// <summary>
    /// Commits the transaction asynchronously.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls the transaction back.
    /// </summary>
    void Rollback();

    /// <summary>
    /// Rolls the transaction back asynchronously.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a named savepoint inside the current transaction.
    /// </summary>
    void CreateSavepoint(string name);

    /// <summary>
    /// Creates a named savepoint inside the current transaction asynchronously.
    /// </summary>
    Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls the current transaction back to a named savepoint.
    /// </summary>
    void RollbackToSavepoint(string name);

    /// <summary>
    /// Rolls the current transaction back to a named savepoint asynchronously.
    /// </summary>
    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a named savepoint.
    /// </summary>
    void ReleaseSavepoint(string name);

    /// <summary>
    /// Releases a named savepoint asynchronously.
    /// </summary>
    Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default);
}
