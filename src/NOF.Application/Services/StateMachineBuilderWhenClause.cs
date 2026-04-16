namespace NOF.Application;

internal class StateMachineBuilderWhenClause<TState, TNotification> : IStateMachineBuilderWhenClause<TState, TNotification>
    where TState : struct, Enum
    where TNotification : class
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
