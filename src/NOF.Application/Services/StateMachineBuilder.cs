using NOF.Contract;

namespace NOF.Application;

internal sealed class BuildableStateMachineBuilder<TState> : IStateMachineBuilder<TState>, IBuildableStateMachineBuilder
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
