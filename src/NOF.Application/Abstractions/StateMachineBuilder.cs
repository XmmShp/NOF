using NOF.Application.Annotations;

namespace NOF;

/// <summary>
/// Provides a fluent API to configure state machine behavior for a given state type and context.
/// </summary>
/// <typeparam name="TState">The enum type representing states.</typeparam>
/// <typeparam name="TContext">The context object that holds state and other domain data.</typeparam>
public interface IStateMachineBuilder<TState, TContext> : IStateMachineBuilder
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    /// <summary>
    /// Configures a startup rule that triggers when a specific notification is published before any state is set.
    /// The state machine will transition to <paramref name="initialState"/> and execute associated actions.
    /// </summary>
    /// <typeparam name="TNotification">The notification type that triggers the startup rule.</typeparam>
    /// <param name="initialState">The initial state to transition to upon receiving the notification.</param>
    /// <param name="contextFactory">
    /// A factory function that creates the state machine context from the incoming <typeparamref name="TNotification"/>.
    /// This context will be persisted and used throughout the lifecycle of the state machine instance.
    /// It must not return null.
    /// </param>
    /// <returns>A clause to further configure actions and transitions.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="contextFactory"/> is null
    /// </exception>
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> StartWhen<TNotification>(TState initialState, Func<TNotification, TContext> contextFactory)
        where TNotification : class, INotification;

    /// <summary>
    /// Begins configuration for rules that trigger when the state machine is in a specific state.
    /// </summary>
    /// <param name="state">The source state for the transition rule.</param>
    /// <returns>A clause to specify the triggering notification and subsequent behavior.</returns>
    IStateMachineBuilderOnClause<TState, TContext> On(TState state);

    /// <summary>
    /// Configures how to extract the correlation ID from a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification that carries correlation information.</typeparam>
    /// <param name="correlationIdSelector">
    /// A function that extracts the correlation ID from an instance of <typeparamref name="TNotification"/>.
    /// The returned string must uniquely identify the state machine instance associated with this notification.
    /// </param>
    /// <returns>The same builder instance, allowing for fluent configuration chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="correlationIdSelector"/> is null.
    /// </exception>
    /// <remarks>
    /// This method must be called for every notification type that can initiate or interact with
    /// the state machine. If a notification is received at runtime but no correlation ID selector
    /// was registered for its type, the state machine engine will fail to process it.
    /// </remarks>
    IStateMachineBuilder<TState, TContext> Correlate<TNotification>(Func<TNotification, string> correlationIdSelector)
        where TNotification : class, INotification;
}

public interface IStateMachineBuilderOnClause<in TState, out TContext>
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> When<TNotification>()
        where TNotification : class, INotification;
}

public interface IStateMachineBuilderWhenClause<in TState, out TContext, out TNotification>
    where TNotification : class, INotification
    where TContext : class, IStateMachineContext
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TContext, TNotification> ExecuteAsync(Func<TContext, TNotification, IServiceProvider, CancellationToken, Task> actionFunc);
    void TransitionTo(TState state);
}