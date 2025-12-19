namespace NOF.Application.Internals;

public interface IStateMachineOperation
{
    Type ContextType { get; }
    Type NotificationType { get; }
    Task ExecuteAsync(IStateMachineContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Central registry of all state machine operations, indexed by notification type.
/// Built during application startup from all IStateMachineDefinition instances.
/// </summary>
public interface IStateMachineRegistry
{
    /// <summary>
    /// Gets all startup operations for a given notification type.
    /// Used when no context exists yet.
    /// </summary>
    IReadOnlyList<IStateMachineOperation> GetStartupOperations(Type notificationType);

    /// <summary>
    /// Gets all transfer operations for a given notification type and current context type.
    /// Used when context already exists.
    /// </summary>
    IReadOnlyList<IStateMachineOperation> GetTransferOperations(Type notificationType, Type contextType);
}

public sealed class StateMachineRegistry : IStateMachineRegistry
{
    private readonly Dictionary<Type, List<IStateMachineOperation>> _startupByNotification = [];
    private readonly Dictionary<(Type Notification, Type Context), List<IStateMachineOperation>> _transferByNotifAndContext = [];

    public StateMachineRegistry(IEnumerable<IStateMachineOperation> startupRules, IEnumerable<IStateMachineOperation> transferRules)
    {
        foreach (var rule in startupRules)
        {
            _startupByNotification.GetOrAdd(rule.NotificationType).Add(rule);
        }

        foreach (var rule in transferRules)
        {
            var key = (rule.NotificationType, rule.ContextType);
            _transferByNotifAndContext.GetOrAdd(key).Add(rule);
        }
    }

    public IReadOnlyList<IStateMachineOperation> GetStartupOperations(Type notificationType)
    {
        return _startupByNotification.TryGetValue(notificationType, out var list)
            ? list
            : [];
    }

    public IReadOnlyList<IStateMachineOperation> GetTransferOperations(Type notificationType, Type contextType)
    {
        var key = (notificationType, contextType);
        return _transferByNotifAndContext.TryGetValue(key, out var list)
            ? list
            : [];
    }
}