using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// State machine context entity containing the context, state, and tracing information.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StateMachineContext
{
    /// <summary>
    /// The correlation ID that identifies the state machine instance.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The state machine definition type.
    /// </summary>
    public required Type DefinitionType { get; init; }

    /// <summary>
    /// The state machine context instance.
    /// </summary>
    public required object Context { get; init; }

    /// <summary>
    /// The current state.
    /// </summary>
    public required int State { get; init; }

    /// <summary>
    /// Creates a new state machine context instance.
    /// </summary>
    public static StateMachineContext Create(
        string correlationId,
        Type definitionType,
        object context,
        int state,
        string? traceId = null,
        string? spanId = null)
    {
        return new StateMachineContext
        {
            CorrelationId = correlationId,
            DefinitionType = definitionType,
            Context = context,
            State = state
        };
    }
}

/// <summary>
/// Repository for persisting state machine contexts. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineContextRepository
{
    /// <summary>Finds a state machine context by correlation ID and definition type.</summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="definitionType">The state machine definition type.</param>
    /// <returns>The state machine context, or <c>null</c> if not found.</returns>
    ValueTask<StateMachineContext?> FindAsync(string correlationId, Type definitionType);
    /// <summary>Adds a new state machine context.</summary>
    /// <param name="stateMachineContext">The context to add.</param>
    void Add(StateMachineContext stateMachineContext);
    /// <summary>Updates an existing state machine context.</summary>
    /// <param name="stateMachineContext">The context to update.</param>
    void Update(StateMachineContext stateMachineContext);
}
