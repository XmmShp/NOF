using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

public static class EventPublishingExtensions
{
    extension<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TEvent>(TEvent @event)
    {
        /// <summary>
        /// Publishes the event through NOF's ambient publisher convenience API.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="PublishAsEvent(TEvent, IEventPublisher)"/> when you want an explicit dependency.
        /// </remarks>
        public void PublishAsEvent()
        {
            ArgumentNullException.ThrowIfNull(@event);
            EventPublisher.PublishEvent(@event);
        }

        /// <summary>
        /// Publishes the event with an explicit <see cref="IEventPublisher"/> dependency.
        /// </summary>
        public void PublishAsEvent(IEventPublisher publisher)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(publisher);
            publisher.PublishAsync(@event, typeof(TEvent).GetAllAssignableTypes(), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
