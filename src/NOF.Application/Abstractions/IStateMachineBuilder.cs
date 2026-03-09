using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineBuilder;

/// <summary>
/// Provides a fluent API to configure state machine behavior for a given state type.
/// The framework is only responsible for state transitions; context management is the caller's responsibility.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
public interface IStateMachineBuilder<TState> : IStateMachineBuilder
    where TState : struct, Enum
{
    /// <summary>
    /// Configures a startup rule that triggers when a specific notification is published before any state is set.
    /// The state machine will transition to <paramref name="initialState"/> and execute associated actions.
    /// </summary>
    /// <typeparam name="TNotification">The notification type that triggers the startup rule.</typeparam>
    /// <param name="initialState">The initial state to transition to upon receiving the notification.</param>
    /// <returns>A clause to further configure actions and transitions.</returns>
    IStateMachineBuilderWhenClause<TState, TNotification> StartWhen<TNotification>(TState initialState)
        where TNotification : class, INotification;

    /// <summary>
    /// Begins configuration for rules that trigger when the state machine is in a specific state.
    /// </summary>
    /// <param name="state">The source state for the transition rule.</param>
    /// <returns>A clause to specify the triggering notification and subsequent behavior.</returns>
    IStateMachineBuilderOnClause<TState> On(TState state);

    /// <summary>
    /// Configures how to extract the correlation ID from a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification that carries correlation information.</typeparam>
    /// <param name="correlationIdSelector">
    /// A function that extracts the correlation ID from an instance of <typeparamref name="TNotification"/>.
    /// The returned string must uniquely identify the state machine instance associated with this notification.
    /// </param>
    /// <returns>The same builder instance, allowing for fluent configuration chaining.</returns>
    IStateMachineBuilder<TState> Correlate<TNotification>(Func<TNotification, string> correlationIdSelector)
        where TNotification : class, INotification;
}

internal interface IBuildableStateMachineBuilder
{
    StateMachineBlueprint Build();
}
