using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for state machine definitions. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineDefinition;

/// <summary>
/// Defines a state machine with states and transitions.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
public interface IStateMachineDefinition<TState> : IStateMachineDefinition
    where TState : struct, Enum
{
    /// <summary>Builds the state machine definition using the provided builder.</summary>
    /// <param name="builder">The state machine builder.</param>
    void Build(IStateMachineBuilder<TState> builder);
}
