using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

public static class EventPublishingExtensions
{
    extension<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TEvent>(TEvent @event)
    {
        public void PublishAsEvent()
        {
            ArgumentNullException.ThrowIfNull(@event);
            EventPublisher.PublishEvent(@event);
        }

        public void PublishAsEvent(IEventPublisher publisher)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(publisher);
            publisher.PublishAsync(@event, typeof(TEvent).GetAllAssignableTypes(), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
