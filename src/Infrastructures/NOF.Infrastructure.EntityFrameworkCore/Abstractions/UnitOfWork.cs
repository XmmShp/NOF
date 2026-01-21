using Microsoft.EntityFrameworkCore;

namespace NOF;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly IEventPublisher _publisher;
    private readonly IOutboxMessageRepository _messageRepository;
    private readonly IOutboxMessageCollector _collector;
    private readonly IOutboxPublisher _outboxPublisher;

    public UnitOfWork(
        DbContext dbContext,
        IEventPublisher publisher,
        IOutboxMessageRepository messageRepository,
        IOutboxMessageCollector collector,
        IOutboxPublisher outboxPublisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _messageRepository = messageRepository;
        _collector = collector;
        _outboxPublisher = outboxPublisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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
                }

                var result = await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _collector.Clear();
                if (hasMessage)
                {
                    _outboxPublisher.TriggerImmediateProcessing();
                }

                return result;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
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
            }

            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _collector.Clear();

            return result;
        }
    }
}
