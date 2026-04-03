using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Handler execution context.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InboundContext
{
    /// <summary>
    /// Message instance.
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    /// Handler type.
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    /// Service provider for resolving dependencies during pipeline execution.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Response result (only used for request handlers).
    /// </summary>
    public IResult? Response { get; set; }
}
