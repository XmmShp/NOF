using System.ComponentModel;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineDefinition;

public interface IStateMachineDefinition<TState, TContext> : IStateMachineDefinition
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    void Build(IStateMachineBuilder<TState, TContext> builder);
}
