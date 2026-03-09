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

    /// <returns>The initial state value, or <c>null</c> if no startup rule matched.</returns>
    public abstract Task<int?> StartAsync<TNotification>(TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;

    /// <returns>The new state value after the transition.</returns>
    public abstract Task<int> TransferAsync<TNotification>(int currentState, TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;
}

