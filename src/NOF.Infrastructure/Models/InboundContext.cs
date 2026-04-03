using NOF.Application;
using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Handler execution context
/// Contains metadata during handler execution
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InboundContext
{
    /// <summary>
    /// Message instance
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    /// Handler type
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    /// Execution context for cross-cutting concerns (tenant, user, tracing, headers).
    /// This is the context that gets propagated across requests/operations.
    /// </summary>
    public required IExecutionContext ExecutionContext { get; init; }

    /// <summary>
    /// Service provider for resolving dependencies during pipeline execution.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Response result (only used for Request handlers)
    /// </summary>
    public IResult? Response { get; set; }
}
