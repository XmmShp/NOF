using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineBuilder;

/// <summary>
/// Provides a fluent API to configure state machine behavior for a given state type.
/// The framework is only responsible for state transitions; context management is the caller's responsibility.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
public interface IStateMachineBuilder<TState> : IStateMachineBuilder
    where TState : struct, Enum
{
    /// <summary>
    /// Configures a startup rule that triggers when a specific notification is published before any state is set.
    /// The state machine will transition to <paramref name="initialState"/> and execute associated actions.
    /// </summary>
    /// <typeparam name="TNotification">The notification type that triggers the startup rule.</typeparam>
    /// <param name="initialState">The initial state to transition to upon receiving the notification.</param>
    /// <returns>A clause to further configure actions and transitions.</returns>
    IStateMachineBuilderWhenClause<TState, TNotification> StartWhen<TNotification>(TState initialState)
        where TNotification : class, INotification;

    /// <summary>
    /// Begins configuration for rules that trigger when the state machine is in a specific state.
    /// </summary>
    /// <param name="state">The source state for the transition rule.</param>
    /// <returns>A clause to specify the triggering notification and subsequent behavior.</returns>
    IStateMachineBuilderOnClause<TState> On(TState state);

    /// <summary>
    /// Configures how to extract the correlation ID from a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification that carries correlation information.</typeparam>
    /// <param name="correlationIdSelector">
    /// A function that extracts the correlation ID from an instance of <typeparamref name="TNotification"/>.
    /// The returned string must uniquely identify the state machine instance associated with this notification.
    /// </param>
    /// <returns>The same builder instance, allowing for fluent configuration chaining.</returns>
    IStateMachineBuilder<TState> Correlate<TNotification>(Func<TNotification, string> correlationIdSelector)
        where TNotification : class, INotification;
}

public interface IStateMachineBuilderOnClause<in TState>
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TNotification> When<TNotification>()
        where TNotification : class, INotification;
}

public interface IStateMachineBuilderWhenClause<in TState, out TNotification>
    where TNotification : class, INotification
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TNotification> ExecuteAsync(Func<TNotification, IServiceProvider, CancellationToken, Task> actionFunc);
    void TransitionTo(TState state);

    /// <summary>Executes a synchronous action when the state machine transition is triggered.</summary>
    IStateMachineBuilderWhenClause<TState, TNotification> Execute(Action<TNotification, IServiceProvider> action)
    {
        return ExecuteAsync((notification, sp, _) =>
        {
            action(notification, sp);
            return Task.CompletedTask;
        });
    }

    /// <summary>Sends a command asynchronously when the transition is triggered.</summary>
    IStateMachineBuilderWhenClause<TState, TNotification> SendCommandAsync<TCommand>(Func<TNotification, TCommand> commandFactory)
        where TCommand : class, ICommand
    {
        return ExecuteAsync(async (notification, sp, cancellationToken) =>
        {
            var commandSender = sp.GetRequiredService<ICommandSender>();
            await commandSender.SendAsync(commandFactory(notification), cancellationToken: cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>Publishes a notification asynchronously when the transition is triggered.</summary>
    IStateMachineBuilderWhenClause<TState, TNotification> PublishNotificationAsync<TAnotherNotification>(Func<TNotification, TAnotherNotification> notificationFactory)
        where TAnotherNotification : class, INotification
    {
        return ExecuteAsync(async (notification, sp, cancellationToken) =>
        {
            var notificationPublisher = sp.GetRequiredService<INotificationPublisher>();
            await notificationPublisher.PublishAsync(notificationFactory(notification), cancellationToken: cancellationToken).ConfigureAwait(false);
        });
    }
}

internal interface IStateMachineBuilderInternal
{
    StateMachineBlueprint Build();
}

internal sealed class StateMachineBuilder<TState> : IStateMachineBuilder<TState>, IStateMachineBuilderInternal
    where TState : struct, Enum
{
    #region Internal Helpers
    internal delegate Task Operation(INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    internal class StateMachineOperation
    {
        public TState? TargetState;
        public List<Operation> Operations { get; } = [];

        public async Task<TState> ExecuteAsync(TState currentState, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            foreach (var op in Operations)
            {
                await op(notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            }

            return TargetState ?? currentState;
        }
    }

    internal sealed class BuilderBlueprint : StateMachineBlueprint
    {
        public Dictionary<Type, StateMachineOperation> StartOperations { get; } = [];
        public Dictionary<TState, Dictionary<Type, StateMachineOperation>> TransferOperations { get; } = [];

        public override async Task<int?> StartAsync<TNotification>(TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            if (!StartOperations.TryGetValue(typeof(TNotification), out var operation))
            {
                return null;
            }

            var state = await operation.ExecuteAsync(default, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(state);
        }

        public override async Task<int> TransferAsync<TNotification>(int currentState, TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var typedState = (TState)(object)currentState;
            if (!TransferOperations.TryGetValue(typedState, out var operations))
            {
                return currentState;
            }

            if (!operations.TryGetValue(typeof(TNotification), out var operation))
            {
                return currentState;
            }

            var newState = await operation.ExecuteAsync(typedState, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(newState);
        }
    }
    #endregion

    private readonly BuilderBlueprint _blueprint = new();

    public IStateMachineBuilderWhenClause<TState, TNotification> StartWhen<TNotification>(TState initialState)
        where TNotification : class, INotification
    {
        var notificationType = typeof(TNotification);
        if (_blueprint.StartOperations.ContainsKey(notificationType))
        {
            throw new InvalidOperationException($"Startup rule for notification '{notificationType}' already exists.");
        }

        _blueprint.ObservedTypes.Add(notificationType);
        var operation = new StateMachineOperation { TargetState = initialState };
        _blueprint.StartOperations.Add(notificationType, operation);

        return new StateMachineBuilderWhenClause<TState, TNotification>(
            _ => throw new InvalidOperationException(
                "Startup rules do not support explicit 'TransitionTo'. " +
                "The target state is fixed as the 'initialState' passed to 'StartWhen<TNotification>'."),
            actionFunc => operation.Operations.Add((n, sp, ct) => actionFunc((TNotification)n, sp, ct)));
    }

    public IStateMachineBuilderOnClause<TState> On(TState state)
    {
        if (!_blueprint.TransferOperations.TryGetValue(state, out var dict))
        {
            dict = [];
            _blueprint.TransferOperations.Add(state, dict);
        }

        return new StateMachineBuilderOnClause<TState>(state, GetFactory);

        (Action<TState> SetTargetState, Action<Operation> AddAction) GetFactory(Type notificationType)
        {
            if (dict.ContainsKey(notificationType))
            {
                throw new InvalidOperationException($"Transfer rule for notification '{notificationType}' already exists.");
            }

            _blueprint.ObservedTypes.Add(notificationType);
            var operation = new StateMachineOperation();
            dict.Add(notificationType, operation);

            return (SetTargetState, AddAction);

            void SetTargetState(TState targetState)
            {
                if (operation.TargetState is not null)
                {
                    throw new InvalidOperationException(
                        $"Transition target state for notification '{notificationType.Name}' " +
                        $"from source state '{state}' has already been set to '{operation.TargetState.Value}'. " +
                        $"Each 'When<{notificationType.Name}>().TransitionTo(...)' can only be called once.");
                }

                operation.TargetState = targetState;
            }

            void AddAction(Operation actionFunc) => operation.Operations.Add(actionFunc);
        }
    }

    public IStateMachineBuilder<TState> Correlate<TNotification>(Func<TNotification, string> correlationIdSelector)
        where TNotification : class, INotification
    {
        var notificationType = typeof(TNotification);
        if (_blueprint.CorrelationIdSelectors.ContainsKey(notificationType))
        {
            throw new InvalidOperationException(
                $"Correlation ID selector for notification type '{typeof(TNotification).Name}' has already been configured. " +
                "Each notification type can only be associated with one correlation ID extraction strategy.");
        }

        _blueprint.CorrelationIdSelectors.Add(notificationType, o => correlationIdSelector((TNotification)o));
        return this;
    }

    public StateMachineBlueprint Build()
    {
        var configuredTypes = new HashSet<Type>(_blueprint.CorrelationIdSelectors.Keys);
        var requiredTypes = new HashSet<Type>(_blueprint.ObservedNotificationTypes);

        if (configuredTypes.SetEquals(requiredTypes))
        {
            return _blueprint;
        }

        var missing = requiredTypes.Except(configuredTypes).ToList();
        var extra = configuredTypes.Except(requiredTypes).ToList();

        var message = "Correlation ID configuration does not match the set of observed notification types.";
        if (missing.Count != 0)
        {
            message += $" Missing for: {string.Join(", ", missing.Select(t => $"'{t.Name}'"))}.";
        }

        if (extra.Count != 0)
        {
            message += $" Unexpectedly configured for: {string.Join(", ", extra.Select(t => $"'{t.Name}'"))}.";
        }

        throw new InvalidOperationException(message);
    }
}

internal class StateMachineBuilderOnClause<TState> : IStateMachineBuilderOnClause<TState>
    where TState : struct, Enum
{
    private readonly TState _state;
    private readonly Func<Type, (Action<TState> SetTargetState, Action<StateMachineBuilder<TState>.Operation> AddAction)> _factory;

    public StateMachineBuilderOnClause(TState state, Func<Type, (Action<TState> SetTargetState, Action<StateMachineBuilder<TState>.Operation> AddAction)> factory)
    {
        _state = state;
        _factory = factory;
    }

    public IStateMachineBuilderWhenClause<TState, TNotification> When<TNotification>()
        where TNotification : class, INotification
    {
        var (setTargetState, addAction) = _factory(typeof(TNotification));
        return new StateMachineBuilderWhenClause<TState, TNotification>(setTargetState,
            actionFunc => addAction((n, sp, ct) => actionFunc((TNotification)n, sp, ct)));
    }
}

internal class StateMachineBuilderWhenClause<TState, TNotification> : IStateMachineBuilderWhenClause<TState, TNotification>
    where TState : struct, Enum
    where TNotification : class, INotification
{
    private readonly Action<TState> _setTargetState;
    private readonly Action<Func<TNotification, IServiceProvider, CancellationToken, Task>> _addAction;

    public StateMachineBuilderWhenClause(
        Action<TState> setTargetState,
        Action<Func<TNotification, IServiceProvider, CancellationToken, Task>> addAction)
    {
        ArgumentNullException.ThrowIfNull(setTargetState);
        ArgumentNullException.ThrowIfNull(addAction);
        _setTargetState = setTargetState;
        _addAction = addAction;
    }

    public IStateMachineBuilderWhenClause<TState, TNotification> ExecuteAsync(Func<TNotification, IServiceProvider, CancellationToken, Task> actionFunc)
    {
        ArgumentNullException.ThrowIfNull(actionFunc);
        _addAction(actionFunc);
        return this;
    }

    public void TransitionTo(TState state) => _setTargetState(state);
}
