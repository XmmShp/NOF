using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal class EFCoreUnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly ITransactionManager _transactionManager;
    private readonly IEventPublisher _publisher;
    private readonly IOutboxMessageRepository _messageRepository;
    private readonly IOutboxMessageCollector _collector;

    public EFCoreUnitOfWork(
        DbContext dbContext,
        ITransactionManager transactionManager,
        IEventPublisher publisher,
        IOutboxMessageRepository messageRepository,
        IOutboxMessageCollector collector)
    {
        _dbContext = dbContext;
        _transactionManager = transactionManager;
        _publisher = publisher;
        _messageRepository = messageRepository;
        _collector = collector;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await using var tx = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            var domainEvents = _dbContext.ChangeTracker.Entries<IAggregateRoot>()
                .Select(e => e.Entity)
                .SelectMany(e => { var events = e.Events.ToList(); e.ClearEvents(); return events; }).ToList();

            foreach (var domainEvent in domainEvents)
            {
                await _publisher.PublishAsync(domainEvent, cancellationToken);
            }

            var messages = _collector.GetMessages();

            var hasMessage = messages.Count > 0;

            if (hasMessage)
            {
                _messageRepository.Add(messages, cancellationToken);
                _collector.Clear();
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
