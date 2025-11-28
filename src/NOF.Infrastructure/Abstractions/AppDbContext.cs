using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;

namespace NOF;

public abstract class AppDbContext : DbContext
{
    protected readonly IScopedMediator Mediator;

    protected AppDbContext(DbContextOptions options, IScopedMediator mediator) : base(options)
    {
        Mediator = mediator;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(e => { var events = e.Events.ToList(); e.ClearEvents(); return events; }).ToList();

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await Mediator.Publish(domainEvent as object, cancellationToken);
            }
            catch
            {
                // Ignore
            }
        }

        return result;
    }
}
