using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object payload, Type[] eventTypes, CancellationToken cancellationToken);
}

public static class EventPublisherExtensions
{
    extension(IEventPublisher publisher)
    {
        public Task PublishAsync(
            object payload,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(runtimeType);
            return publisher.PublishAsync(payload, DispatchTypeUtilities.GetSelfAndBaseTypesAndInterfaces(runtimeType), cancellationToken);
        }

        public Task PublishAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(
        TPayload payload,
        CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            return publisher.PublishAsync(payload, typeof(TPayload), cancellationToken);
        }
    }
}