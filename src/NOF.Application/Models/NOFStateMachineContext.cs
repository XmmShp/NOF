using NOF.Contract;
using NOF.Domain;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// State machine context entity containing the context, state, and tracing information.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NOFStateMachineContext : AggregateRoot, ICloneable
{
    /// <summary>
    /// The correlation ID that identifies the state machine instance.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The persisted state machine definition type name.
    /// </summary>
    public required string DefinitionTypeName { get; init; }

    /// <summary>
    /// The current state.
    /// </summary>
    public required int State { get; set; }

    /// <summary>
    /// The tracing information.
    /// </summary>
    public TracingInfo? TracingInfo { get; set; }

    /// <summary>
    /// Creates a new state machine context instance.
    /// </summary>
    public static NOFStateMachineContext Create(
        string correlationId,
        string definitionTypeName,
        int state,
        TracingInfo? tracingInfo = null)
    {
        return new NOFStateMachineContext
        {
            CorrelationId = correlationId,
            DefinitionTypeName = definitionTypeName,
            State = state,
            TracingInfo = tracingInfo
        };
    }

    public object Clone()
        => new NOFStateMachineContext
        {
            CorrelationId = CorrelationId,
            DefinitionTypeName = DefinitionTypeName,
            State = State,
            TracingInfo = TracingInfo
        };
}
