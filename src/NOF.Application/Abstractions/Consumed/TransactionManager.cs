using System.Data;

namespace NOF.Application;

/// <summary>
/// Manages database transactions.
/// </summary>
public interface ITransactionManager
{
    /// <summary>Begins a new database transaction.</summary>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transaction handle.</returns>
    Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an active database transaction.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);
    /// <summary>Rolls back the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
