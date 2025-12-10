using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly IScopedMediator _mediator;
    private readonly ILogger<UnitOfWork> _logger;
    public UnitOfWork(DbContext dbContext, IScopedMediator mediator, ILogger<UnitOfWork> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var domainEvents = _dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(e => { var events = e.Events.ToList(); e.ClearEvents(); return events; }).ToList();

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _mediator.Publish(domainEvent as object, cancellationToken);
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

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
