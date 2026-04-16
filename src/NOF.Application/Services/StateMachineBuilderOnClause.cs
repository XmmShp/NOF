namespace NOF.Application;

internal class StateMachineBuilderOnClause<TState> : IStateMachineBuilderOnClause<TState>
    where TState : struct, Enum
{
    private readonly TState _state;
    private readonly Func<Type, (Action<TState> SetTargetState, Action<BuildableStateMachineBuilder<TState>.Operation> AddAction)> _factory;

    public StateMachineBuilderOnClause(TState state, Func<Type, (Action<TState> SetTargetState, Action<BuildableStateMachineBuilder<TState>.Operation> AddAction)> factory)
    {
        _state = state;
        _factory = factory;
    }

    public IStateMachineBuilderWhenClause<TState, TNotification> When<TNotification>()
        where TNotification : class
    {
        var (setTargetState, addAction) = _factory(typeof(TNotification));
        return new StateMachineBuilderWhenClause<TState, TNotification>(setTargetState,
            actionFunc => addAction((n, sp, ct) => actionFunc((TNotification)n, sp, ct)));
    }
}
