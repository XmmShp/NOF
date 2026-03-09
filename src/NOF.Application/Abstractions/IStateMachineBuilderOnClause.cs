using NOF.Contract;

namespace NOF.Application;

public interface IStateMachineBuilderOnClause<in TState>
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TNotification> When<TNotification>()
        where TNotification : class, INotification;
}
