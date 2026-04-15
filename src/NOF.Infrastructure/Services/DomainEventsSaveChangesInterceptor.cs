using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Infrastructure;

/// <summary>
/// Publishes domain events from aggregates tracked by the DbContext after a successful SaveChanges.
/// This replaces the previous IUnitOfWork-based dispatch flow.
/// </summary>
internal sealed class DomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IEventPublisher _publisher;

    // Interceptor instance is created per DbContext by NOFDbContextFactory, so a field is enough.
    private List<object>? _pendingEvents;

    public DomainEventsSaveChangesInterceptor(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        var events = _pendingEvents;
        _pendingEvents = null;

        // Publish after SaveChanges succeeded. If SaveChanges fails, we keep events on aggregates.
        if (ctx is not null && events is { Count: > 0 })
        {
            foreach (var domainEvent in events)
            {
                await _publisher.PublishAsync(domainEvent, domainEvent.GetType(), cancellationToken).ConfigureAwait(false);
            }

            foreach (var entry in ctx.ChangeTracker.Entries<IAggregateRoot>())
            {
                entry.Entity.Events.Clear();
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingEvents = null;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingEvents = null;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureEvents(DbContext? ctx)
    {
        if (ctx is null)
        {
            return;
        }

        // If SaveChanges is called multiple times, re-capture; any events already cleared are removed in SavedChangesAsync.
        _pendingEvents = ctx.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .SelectMany(entity => entity.Events)
            .ToList();
    }
}
