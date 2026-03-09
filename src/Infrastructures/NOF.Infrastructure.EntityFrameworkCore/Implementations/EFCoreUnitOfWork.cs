using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal class EFCoreUnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly ITransactionManager _transactionManager;
    private readonly IEventPublisher _publisher;

    public EFCoreUnitOfWork(
        DbContext dbContext,
        ITransactionManager transactionManager,
        IEventPublisher publisher)
    {
        _dbContext = dbContext;
        _transactionManager = transactionManager;
        _publisher = publisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await using var tx = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            var domainEvents = _dbContext.ChangeTracker.Entries<IAggregateRoot>()
                .Select(e => e.Entity)
                .SelectMany(e => { var events = e.Events.ToList(); e.Events.Clear(); return events; }).ToList();

            foreach (var domainEvent in domainEvents)
            {
                await _publisher.PublishAsync(domainEvent, cancellationToken);
            }
            var result = await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
