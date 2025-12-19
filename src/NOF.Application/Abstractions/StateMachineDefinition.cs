using NOF.Application.Internals;

namespace NOF;

public interface IStateMachineDefinition<TState, TContext> : IStateMachineDefinition
    where TContext : class, IStateMachineContext<TState>, new()
    where TState : struct, Enum
{
    void Build(IStateMachineBuilder<TState, TContext> builder);
}
