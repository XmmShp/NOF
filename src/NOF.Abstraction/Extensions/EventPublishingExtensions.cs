using NOF.Contract;
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
        /// Publishes the event through NOF's ambient publisher and forwards the supplied context.
        /// </summary>
        public void PublishAsEvent(Context context)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            EventPublisher.PublishEvent(@event, context);
        }

        /// <summary>
        /// Publishes the event with an explicit <see cref="IEventPublisher"/> dependency.
        /// </summary>
        public void PublishAsEvent(IEventPublisher publisher)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(publisher);
            publisher.PublishAsync(@event, typeof(TEvent).GetAllAssignableTypes(), Context.Empty, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Publishes the event with an explicit publisher and forwards the supplied context.
        /// </summary>
        public void PublishAsEvent(IEventPublisher publisher, Context context)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(context);
            publisher.PublishAsync(@event, typeof(TEvent).GetAllAssignableTypes(), context, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
