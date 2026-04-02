using NOF.Application;
using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Context for outbound message pipeline execution.
/// Contains the message being sent and the headers to be attached.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboundContext
{
    /// <summary>
    /// The outbound message (command, notification, or request payload).
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    /// Execution context for cross-cutting concerns (tenant, user, tracing, headers).
    /// This is the context that gets propagated across requests/operations.
    /// </summary>
    public required IExecutionContext ExecutionContext { get; init; }

    /// <summary>
    /// Response result set by the rider or by outbound middleware that short-circuits the pipeline.
    /// Mirrors <see cref="InboundContext.Response"/>.
    /// </summary>
    public IResult? Response { get; set; }
}
