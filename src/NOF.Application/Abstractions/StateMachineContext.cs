using NOF.Application.Internals;

namespace NOF;

public interface IStateMachineContext<TState> : IStateMachineContext
    where TState : struct, Enum
{
    TState State { get; set; }
}