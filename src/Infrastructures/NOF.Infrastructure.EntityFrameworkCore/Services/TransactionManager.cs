using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

namespace NOF;

/// <summary>
/// Entity Framework Core 事务管理器实现
/// </summary>
internal sealed class TransactionManager : ITransactionManager
{
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<TransactionManager> _logger;

    public TransactionManager(NOFDbContext dbContext, ILogger<TransactionManager> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Beginning transaction with isolation level {IsolationLevel}", isolationLevel);

        var efTransaction = await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

        return new EfCoreTransaction(efTransaction, _logger);
    }

    /// <summary>
    /// Entity Framework Core 事务包装器
    /// </summary>
    private sealed class EfCoreTransaction : ITransaction
    {
        private readonly IDbContextTransaction _efTransaction;
        private readonly ILogger<TransactionManager> _logger;
        private bool _disposed = false;

        public EfCoreTransaction(IDbContextTransaction efTransaction, ILogger<TransactionManager> logger)
        {
            _efTransaction = efTransaction;
            _logger = logger;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EfCoreTransaction));
            }

            _logger.LogDebug("Committing transaction");

            await _efTransaction.CommitAsync(cancellationToken);

            _logger.LogDebug("Transaction committed successfully");
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EfCoreTransaction));
            }

            _logger.LogDebug("Rolling back transaction");

            await _efTransaction.RollbackAsync(cancellationToken);

            _logger.LogDebug("Transaction rolled back");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _efTransaction.DisposeAsync();
                _disposed = true;
            }
        }
    }
}
