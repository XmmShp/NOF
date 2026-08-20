using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object payload, Type[] eventTypes, Context context, CancellationToken cancellationToken);
}

public static class EventPublisherExtensions
{
    extension(IEventPublisher publisher)
    {
        public Task PublishAsync(
            object payload,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
            Context context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(runtimeType);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(payload, runtimeType.GetAllAssignableTypes(), context, cancellationToken);
        }

        public Task PublishAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(
            TPayload payload,
            Context context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(payload, typeof(TPayload), context, cancellationToken);
        }

        public Task PublishAsync(
            object payload,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
            CancellationToken cancellationToken = default)
            => publisher.PublishAsync(payload, runtimeType, Context.Empty, cancellationToken);

        public Task PublishAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(
            TPayload payload,
            CancellationToken cancellationToken = default)
            => publisher.PublishAsync(payload, Context.Empty, cancellationToken);
    }
}
