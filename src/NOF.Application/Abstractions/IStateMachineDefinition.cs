using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Defines a state machine with states and transitions.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
public interface IStateMachineDefinition<TState>
    where TState : struct, Enum
{
    /// <summary>Builds the state machine definition using the provided builder.</summary>
    /// <param name="builder">The state machine builder.</param>
    void Build(IStateMachineBuilder<TState> builder);
}
