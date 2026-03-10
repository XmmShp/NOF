using Microsoft.Extensions.Logging;
using NOF.Application;
using System.Data;

namespace NOF.Infrastructure.Core;

internal sealed class InMemoryTransactionManager : ITransactionManager
{
    private readonly InMemoryPersistenceStore _store;
    private readonly ILogger<InMemoryTransactionManager> _logger;
    private readonly Stack<InMemoryTransaction> _transactions = new();

    public InMemoryTransactionManager(InMemoryPersistenceStore store, ILogger<InMemoryTransactionManager> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        var snapshot = _store.CaptureSnapshot();
        var transaction = new InMemoryTransaction(this, snapshot, _transactions.Count == 0, isolationLevel);
        _transactions.Push(transaction);
        _logger.LogWarning("Using in-memory transaction manager. Transactions are process-local snapshots only and are not durable or truly atomic. IsolationLevel: {IsolationLevel}", isolationLevel);
        return Task.FromResult<ITransaction>(transaction);
    }

    private Task CompleteAsync(InMemoryTransaction transaction, bool commit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_transactions.Count == 0 || !ReferenceEquals(_transactions.Peek(), transaction))
        {
            throw new InvalidOperationException("Transaction completion order mismatch. Transactions must be completed in LIFO order.");
        }

        _transactions.Pop();

        if (!commit)
        {
            _store.RestoreSnapshot(transaction.Snapshot);
        }

        return Task.CompletedTask;
    }

    private sealed class InMemoryTransaction : ITransaction
    {
        private readonly InMemoryTransactionManager _manager;
        private bool _completed;
        private bool _disposed;

        public InMemoryTransaction(InMemoryTransactionManager manager, InMemoryPersistenceStoreSnapshot snapshot, bool isRootTransaction, IsolationLevel isolationLevel)
        {
            _manager = manager;
            Snapshot = snapshot;
            IsRootTransaction = isRootTransaction;
            IsolationLevel = isolationLevel;
        }

        public InMemoryPersistenceStoreSnapshot Snapshot { get; }

        public bool IsRootTransaction { get; }

        public IsolationLevel IsolationLevel { get; }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(InMemoryTransaction));
            if (_completed)
            {
                return;
            }

            await _manager.CompleteAsync(this, commit: true, cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(InMemoryTransaction));
            if (_completed)
            {
                return;
            }

            await _manager.CompleteAsync(this, commit: false, cancellationToken);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (!_completed)
            {
                await _manager.CompleteAsync(this, commit: false, CancellationToken.None);
                _completed = true;
            }

            _disposed = true;
        }
    }
}
