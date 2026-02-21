using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class StateMachineBlueprint
{
    public Type DefinitionType
    {
        get
        {
            ArgumentNullException.ThrowIfNull(field);
            return field;
        }
        set;
    } = null!;

    public HashSet<Type> ObservedTypes { get; } = [];
    public Dictionary<Type, Func<object, string>> CorrelationIdSelectors { get; } = [];

    public IReadOnlySet<Type> ObservedNotificationTypes => ObservedTypes.AsReadOnly();

    public string? GetCorrelationId<TNotification>(TNotification notification)
        where TNotification : class, INotification
    {
        return CorrelationIdSelectors.TryGetValue(typeof(TNotification), out var selector)
            ? selector(notification)
            : null;
    }

    public abstract Task<StatefulStateMachineContext?> StartAsync<TNotification>(TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;
    public abstract Task TransferAsync<TNotification>(StatefulStateMachineContext context, TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;
}

