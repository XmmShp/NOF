using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<UnitOfWork> _logger;
    public UnitOfWork(DbContext dbContext, IEventPublisher publisher, ILogger<UnitOfWork> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var domainEvents = _dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(e => { var events = e.Events.ToList(); e.ClearEvents(); return events; }).ToList();

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _publisher.PublishAsync(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "An exception has occured when publishing event: {Message}", ex.Message);
                }

                throw;
            }
        }

        return result;
    }
}
