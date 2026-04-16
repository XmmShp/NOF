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
    }
}
