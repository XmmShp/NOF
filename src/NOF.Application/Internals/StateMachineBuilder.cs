namespace NOF.Application.Internals;

public interface IStateMachineBuilder
{
    public (IReadOnlyList<IStateMachineOperation> StartupRules, IReadOnlyList<IStateMachineOperation> TransferRules) Build();
}

/// <summary>
/// Provides a fluent API to configure state machine behavior for a given state type and context.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
/// <typeparam name="TContext">The context object that holds state and other domain data.</typeparam>
public interface IStateMachineBuilder<TState, TContext> : IStateMachineBuilder
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    /// <summary>
    /// Configures a startup rule that triggers when a specific notification is published before any state is set.
    /// The state machine will transition to <paramref name="initialState"/> and execute associated actions.
    /// </summary>
    /// <typeparam name="TNotification">The notification type that triggers the startup rule.</typeparam>
    /// <param name="initialState">The initial state to transition to upon receiving the notification.</param>
    /// <returns>A clause to further configure actions and transitions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="initialState"/> is invalid (should not occur for enums).</exception>
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> StartWhen<TNotification>(TState initialState)
        where TNotification : class, INotification;

    /// <summary>
    /// Begins configuration for rules that trigger when the state machine is in a specific state.
    /// </summary>
    /// <param name="state">The source state for the transition rule.</param>
    /// <returns>A clause to specify the triggering notification and subsequent behavior.</returns>
    IStateMachineBuilderOnClause<TState, TContext> On(TState state);
}

public class StateMachineBuilder<TState, TContext> : IStateMachineBuilder<TState, TContext>
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    private sealed class StartupStateMachineOperation : IStateMachineOperation
    {
        public StartupStateMachineOperation(Type notificationType)
        {
            NotificationType = notificationType;
        }

        public Type ContextType => typeof(TContext);
        public Type NotificationType { get; }

        public required TState TargetState { get; set; }
        public List<Func<TContext, INotification, IServiceProvider, CancellationToken, Task>> Actions { get; } = [];

        public async Task ExecuteAsync(IStateMachineContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var typedContext = (TContext)context;
            typedContext.State = TargetState;

            foreach (var action in Actions)
            {
                await action(typedContext, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }
    private readonly Dictionary<Type, StartupStateMachineOperation> _startRules = [];

    internal sealed class TransferStateMachineOperation : IStateMachineOperation
    {
        public TransferStateMachineOperation(Type notificationType)
        {
            NotificationType = notificationType;
        }

        public Type ContextType => typeof(TContext);
        public Type NotificationType { get; }

        public TState? TargetState { get; set; }
        public List<Func<TContext, INotification, IServiceProvider, CancellationToken, Task>> Actions { get; } = [];

        public async Task ExecuteAsync(IStateMachineContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var typedContext = (TContext)context;
            foreach (var action in Actions)
            {
                await action(typedContext, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            }

            if (TargetState.HasValue)
            {
                typedContext.State = TargetState.Value;
            }
        }
    }
    // State -> NotificationType -> Rules
    private readonly Dictionary<TState, Dictionary<Type, TransferStateMachineOperation>> _transferRules = [];

    public IStateMachineBuilderWhenClause<TState, TContext, TNotification> StartWhen<TNotification>(TState initialState)
        where TNotification : class, INotification
    {
        var notificationType = typeof(TNotification);
        if (!_startRules.TryGetValue(notificationType, out var value))
        {
            value = new StartupStateMachineOperation(typeof(TNotification)) { TargetState = initialState };
            _startRules.Add(notificationType, value);
        }
        else
        {
            throw new InvalidOperationException($"Startup rule for notification '{notificationType}' already exists.");
        }

        var clause = new StateMachineBuilderWhenClause<TState, TContext, TNotification>(SetTargetState, AddAction);
        return clause;

        static void SetTargetState(TState state)
        {
            throw new InvalidOperationException(
                $"Startup rules do not support explicit 'TransitionTo'. " +
                $"The target state is fixed as the 'initialState' passed to 'StartWhen<TNotification>'." +
                $" If you need a different target, call 'StartWhen' with that state.");
        }

        void AddAction(Func<TContext, TNotification, IServiceProvider, CancellationToken, Task> actionFunc)
        {
            value.Actions.Add((context, notification, serviceProvider, cancellationToken) => actionFunc(context, (TNotification)notification, serviceProvider, cancellationToken));
        }
    }

    public IStateMachineBuilderOnClause<TState, TContext> On(TState state)
    {
        if (!_transferRules.TryGetValue(state, out var dict))
        {
            dict = new Dictionary<Type, TransferStateMachineOperation>();
            _transferRules.Add(state, dict);
        }

        return new StateMachineBuilderOnClause<TState, TContext>(state, GetFactory);

        (Action<TState> SetTargetState, Action<Func<TContext, INotification, IServiceProvider, CancellationToken, Task>> AddAction) GetFactory(Type notificationType)
        {
            if (!dict.TryGetValue(notificationType, out var slot))
            {
                slot = new TransferStateMachineOperation(notificationType);
                dict.Add(notificationType, slot);
            }

            return (SetTargetState, AddAction);

            void SetTargetState(TState targetState)
            {
                if (slot.TargetState.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Transition target state for notification '{notificationType.Name}' " +
                        $"from source state '{state}' has already been set to '{slot.TargetState.Value}'. " +
                        $"Each 'When<{notificationType.Name}>().TransitionTo(...)' can only be called once.");
                }
                slot.TargetState = targetState;
            }

            void AddAction(Func<TContext, INotification, IServiceProvider, CancellationToken, Task> actionFunc)
            {
                slot.Actions.Add(actionFunc);
            }
        }
    }

    public (IReadOnlyList<IStateMachineOperation> StartupRules, IReadOnlyList<IStateMachineOperation> TransferRules) Build()
    {
        var startup = _startRules.Select(kv => kv.Value).ToList().AsReadOnly();
        var transfer = _transferRules.SelectMany(kv => kv.Value.Select(kv2 => kv2.Value)).ToList().AsReadOnly();
        return (startup, transfer);
    }
}

#region OnClause
public interface IStateMachineBuilderOnClause<in TState, out TContext>
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> When<TNotification>()
        where TNotification : class, INotification;
}

internal class StateMachineBuilderOnClause<TState, TContext> : IStateMachineBuilderOnClause<TState, TContext>
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    private readonly TState _state;

    private readonly Func<Type, (Action<TState> SetTargetState, Action<Func<TContext, INotification, IServiceProvider, CancellationToken, Task>> AddAction)> _factory;
    public StateMachineBuilderOnClause(TState state, Func<Type, (Action<TState> SetTargetState, Action<Func<TContext, INotification, IServiceProvider, CancellationToken, Task>> AddAction)> factory)
    {
        _state = state;
        _factory = factory;
    }

    public IStateMachineBuilderWhenClause<TState, TContext, TNotification> When<TNotification>()
        where TNotification : class, INotification
    {
        var (setTargetState, addAction) = _factory(typeof(TNotification));
        var clause = new StateMachineBuilderWhenClause<TState, TContext, TNotification>(setTargetState, f =>
        {
            addAction(Func);
            return;

            Task Func(TContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
            {
                if (context.State.Equals(_state))
                {
                    return f(context, (TNotification)notification, serviceProvider, cancellationToken);
                }

                return Task.CompletedTask;
            }
        });
        return clause;
    }
}
#endregion

#region WhenClause
public interface IStateMachineBuilderWhenClause<in TState, out TContext, out TNotification>
    where TNotification : class, INotification
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> ExecuteAsync(Func<TContext, TNotification, IServiceProvider, CancellationToken, Task> actionFunc);
    void TransitionTo(TState state);
}

internal class StateMachineBuilderWhenClause<TState, TContext, TNotification> : IStateMachineBuilderWhenClause<TState, TContext, TNotification>
    where TState : struct, Enum
    where TNotification : class, INotification
    where TContext : class, IStateMachineContext<TState>, new()
{
    private readonly Action<TState> _setTargetState;
    private readonly Action<Func<TContext, TNotification, IServiceProvider, CancellationToken, Task>> _addAction;

    public StateMachineBuilderWhenClause(Action<TState> setTargetState, Action<Func<TContext, TNotification, IServiceProvider, CancellationToken, Task>> addAction)
    {
        ArgumentNullException.ThrowIfNull(setTargetState);
        ArgumentNullException.ThrowIfNull(addAction);
        _setTargetState = setTargetState;
        _addAction = addAction;
    }

    public IStateMachineBuilderWhenClause<TState, TContext, TNotification> ExecuteAsync(Func<TContext, TNotification, IServiceProvider, CancellationToken, Task> actionFunc)
    {
        ArgumentNullException.ThrowIfNull(actionFunc);

        _addAction(actionFunc);
        return this;
    }

    public void TransitionTo(TState state)
    {
        _setTargetState(state);
    }
}
#endregion
