namespace NOF.Abstraction;

public static class EventPublisherExtensions
{
    public static Task PublishAsync<TPayload>(
        this IEventPublisher publisher,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(payload);
        return publisher.PublishAsync(payload, typeof(TPayload), cancellationToken);
    }
}
