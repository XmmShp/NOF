namespace NOF.Application.Reflections;

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

    internal HashSet<Type> ObservedTypes { get; } = [];
    internal Dictionary<Type, Func<object, string>> CorrelationIdSelectors { get; } = [];

    public IReadOnlySet<Type> ObservedNotificationTypes => ObservedTypes.AsReadOnly();

    public string? GetCorrelationId<TNotification>(TNotification notification)
        where TNotification : class, INotification
    {
        return CorrelationIdSelectors.TryGetValue(typeof(TNotification), out var selector)
            ? selector(notification)
            : null;
    }

    internal abstract Task<StatefulStateMachineContext?> StartAsync<TNotification>(TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;
    internal abstract Task TransferAsync<TNotification>(StatefulStateMachineContext context, TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TNotification : class, INotification;
}

internal class StatefulStateMachineContext
{
    public int State { get; set; }
    public required IStateMachineContext Context { get; init; }
}