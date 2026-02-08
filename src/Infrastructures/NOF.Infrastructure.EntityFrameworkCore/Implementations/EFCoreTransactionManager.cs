using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Entity Framework Core transaction manager implementation with nested transaction support.
/// </summary>
internal sealed class EFCoreTransactionManager : ITransactionManager
{
    private readonly DbContext _dbContext;
    private readonly ILogger<EFCoreTransactionManager> _logger;
    private readonly Stack<EFCoreTransaction> _transactionStack = new();

    public EFCoreTransactionManager(DbContext dbContext, ILogger<EFCoreTransactionManager> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        if (_transactionStack.Count == 0)
        {
            // Begin root transaction
            _logger.LogDebug("Beginning root transaction with isolation level {IsolationLevel}", isolationLevel);

            var efTransaction = await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            var transaction = new EFCoreTransaction(efTransaction, this, isRootTransaction: true);

            _transactionStack.Push(transaction);
            return transaction;
        }
        else
        {
            // Begin nested transaction (using savepoint)
            _logger.LogDebug("Beginning nested transaction (savepoint)");

            var currentTransaction = _transactionStack.Peek();
            var savepointName = $"sp_{Guid.NewGuid():N}";
            await currentTransaction.CreateSavepointAsync(savepointName, cancellationToken);

            var transaction = new EFCoreTransaction(currentTransaction.EfTransaction, this, isRootTransaction: false, savepointName: savepointName);
            _transactionStack.Push(transaction);

            return transaction;
        }
    }

    private async Task CompleteTransactionAsync(EFCoreTransaction transaction, bool commit, CancellationToken cancellationToken = default)
    {
        if (_transactionStack.Count == 0)
        {
            throw new InvalidOperationException("No active transaction to complete");
        }

        var topTransaction = _transactionStack.Peek();
        if (topTransaction != transaction)
        {
            throw new InvalidOperationException("Transaction completion order mismatch. Transactions must be completed in LIFO order.");
        }

        _transactionStack.Pop();

        if (transaction.IsRootTransaction)
        {
            // Commit or rollback root transaction
            if (commit)
            {
                _logger.LogDebug("Committing root transaction");
                await transaction.EfTransaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Root transaction committed successfully");
            }
            else
            {
                _logger.LogDebug("Rolling back root transaction");
                await transaction.EfTransaction.RollbackAsync(cancellationToken);
                _logger.LogDebug("Root transaction rolled back");
            }
        }
        else
        {
            // Commit or rollback nested transaction (savepoint)
            if (commit)
            {
                _logger.LogDebug("Committing nested transaction (releasing savepoint {SavepointName})", transaction.SavepointName);
                // Nested transaction "commit" only releases the savepoint; actual commit happens at root transaction
                _logger.LogDebug("Nested transaction committed (savepoint released)");
            }
            else
            {
                _logger.LogDebug("Rolling back nested transaction (rolling back to savepoint {SavepointName})", transaction.SavepointName);
                await transaction.RollbackToSavepointAsync(transaction.SavepointName, cancellationToken);
                _logger.LogDebug("Nested transaction rolled back to savepoint");
            }
        }

        await transaction.DisposeAsync();
    }

    /// <summary>
    /// Entity Framework Core transaction wrapper with nested transaction support.
    /// </summary>
    private sealed class EFCoreTransaction : ITransaction
    {
        private readonly EFCoreTransactionManager _manager;
        private bool _disposed;

        public EFCoreTransaction(IDbContextTransaction efTransaction, EFCoreTransactionManager manager, bool isRootTransaction, string? savepointName = null)
        {
            EfTransaction = efTransaction;
            _manager = manager;
            IsRootTransaction = isRootTransaction;
            SavepointName = savepointName;
        }

        public IDbContextTransaction EfTransaction { get; }

        [MemberNotNullWhen(false, nameof(SavepointName))]
        public bool IsRootTransaction { get; }
        public string? SavepointName { get; }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(EFCoreTransaction));
            await _manager.CompleteTransactionAsync(this, commit: true, cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(EFCoreTransaction));
            await _manager.CompleteTransactionAsync(this, commit: false, cancellationToken);
        }

        internal async Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken)
        {
            await EfTransaction.CreateSavepointAsync(savepointName, cancellationToken);
        }

        internal async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken)
        {
            await EfTransaction.RollbackToSavepointAsync(savepointName, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                // If not explicitly committed or rolled back, automatically rollback
                if (_manager._transactionStack.Contains(this))
                {
                    await _manager.CompleteTransactionAsync(this, commit: false);
                }
                else if (IsRootTransaction)
                {
                    await EfTransaction.DisposeAsync();
                }

                _disposed = true;
            }
        }
    }
}
