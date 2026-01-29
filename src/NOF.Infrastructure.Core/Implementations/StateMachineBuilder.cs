using System.ComponentModel;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public class StateMachineBuilder<TState, TContext> : IStateMachineBuilder<TState, TContext>, IStateMachineBuilderInternal
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    #region Internal Helpers
    internal delegate Task Operation(TContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    internal class StateMachineOperation
    {
        public int? TargetState;
        public List<Operation> Operations { get; } = [];

        public async Task ExecuteAsync(StatefulStateMachineContext context, INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            foreach (var operation in Operations)
            {
                await operation((TContext)context.Context, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            }

            if (TargetState is not null)
            {
                if (context.State != TargetState.Value)
                {
                    context.State = TargetState.Value;
                }
            }
        }
    }

    internal sealed class StartStateMachineOperation : StateMachineOperation
    {
        public required Func<object, TContext> Factory { get; init; }
    }
    internal sealed class BuilderBlueprint : StateMachineBlueprint
    {
        public Dictionary<Type, StartStateMachineOperation> StartOperations { get; } = [];
        public Dictionary<int, Dictionary<Type, StateMachineOperation>> TransferOperations { get; } = [];

        public override async Task<StatefulStateMachineContext?> StartAsync<TNotification>(TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            if (!StartOperations.TryGetValue(typeof(TNotification), out var operation))
            {
                return null;
            }

            var context = operation.Factory(notification);
            var statefulContext = new StatefulStateMachineContext { Context = context };
            await operation.ExecuteAsync(statefulContext, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            return statefulContext;
        }

        public override async Task TransferAsync<TNotification>(StatefulStateMachineContext context, TNotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            if (context.Context is not TContext typedContext)
            {
                return;
            }
            if (!TransferOperations.TryGetValue(context.State, out var operations))
            {
                return;
            }
            if (operations.TryGetValue(typeof(TNotification), out var operation))
            {
                await operation.ExecuteAsync(context, notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }
    #endregion

    private readonly BuilderBlueprint _blueprint = new();

    public IStateMachineBuilderWhenClause<TState, TContext, TNotification> StartWhen<TNotification>(TState initialState, Func<TNotification, TContext> contextFactory)
        where TNotification : class, INotification
    {
        var notificationType = typeof(TNotification);
        if (_blueprint.StartOperations.TryGetValue(notificationType, out var operation))
        {
            throw new InvalidOperationException($"Startup rule for notification '{notificationType}' already exists.");
        }

        _blueprint.ObservedTypes.Add(notificationType);
        operation = new StartStateMachineOperation { TargetState = Convert.ToInt32(initialState), Factory = o => contextFactory((TNotification)o) };
        _blueprint.StartOperations.Add(notificationType, operation);
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
            operation.Operations.Add((context, notification, serviceProvider, cancellationToken) => actionFunc(context, (TNotification)notification, serviceProvider, cancellationToken));
        }
    }

    public IStateMachineBuilderOnClause<TState, TContext> On(TState state)
    {
        var stateInt = Convert.ToInt32(state);
        if (!_blueprint.TransferOperations.TryGetValue(stateInt, out var dict))
        {
            dict = [];
            _blueprint.TransferOperations.Add(stateInt, dict);
        }

        return new StateMachineBuilderOnClause<TState, TContext>(state, GetFactory);

        (Action<TState> SetTargetState, Action<Operation> AddAction) GetFactory(Type notificationType)
        {
            if (dict.TryGetValue(notificationType, out var operation))
            {
                throw new InvalidOperationException($"Transfer rule for notification '{notificationType}' already exists.");
            }

            _blueprint.ObservedTypes.Add(notificationType);
            operation = new StateMachineOperation();
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
                operation.TargetState = Convert.ToInt32(targetState);
            }

            void AddAction(Operation actionFunc)
            {
                operation.Operations.Add(actionFunc);
            }
        }
    }

    public IStateMachineBuilder<TState, TContext> Correlate<TNotification>(Func<TNotification, string> correlationIdSelector) where TNotification : class, INotification
    {
        var notificationType = typeof(TNotification);
        if (_blueprint.CorrelationIdSelectors.ContainsKey(notificationType))
        {
            throw new InvalidOperationException($"Correlation ID selector for notification type '{typeof(TNotification).Name}' has already been configured. " +
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

internal class StateMachineBuilderOnClause<TState, TContext> : IStateMachineBuilderOnClause<TState, TContext>
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    private readonly TState _state;

    private readonly Func<Type, (Action<TState> SetTargetState, Action<StateMachineBuilder<TState, TContext>.Operation> AddAction)> _factory;
    public StateMachineBuilderOnClause(TState state, Func<Type, (Action<TState> SetTargetState, Action<StateMachineBuilder<TState, TContext>.Operation> AddAction)> factory)
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
                return f(context, (TNotification)notification, serviceProvider, cancellationToken);
            }
        });
        return clause;
    }
}

internal class StateMachineBuilderWhenClause<TState, TContext, TNotification> : IStateMachineBuilderWhenClause<TState, TContext, TNotification>
    where TState : struct, Enum
    where TNotification : class, INotification
    where TContext : class, IStateMachineContext
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