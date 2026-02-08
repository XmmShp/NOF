using System.ComponentModel;

namespace NOF;

/// <summary>
/// Non-generic marker interface for state machine definitions. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineDefinition;

/// <summary>
/// Defines a state machine with states and transitions.
/// </summary>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TContext">The state machine context type.</typeparam>
public interface IStateMachineDefinition<TState, TContext> : IStateMachineDefinition
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    /// <summary>Builds the state machine definition using the provided builder.</summary>
    /// <param name="builder">The state machine builder.</param>
    void Build(IStateMachineBuilder<TState, TContext> builder);
}
