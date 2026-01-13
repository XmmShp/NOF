using NOF.Application.Annotations;

namespace NOF.Application.Abstractions;

public interface IStateMachineDefinition<TState, TContext> : IStateMachineDefinition
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    void Build(IStateMachineBuilder<TState, TContext> builder);
}
