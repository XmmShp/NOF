using System.ComponentModel;

namespace NOF.Contract;

/// <summary>
/// Context for outbound message pipeline execution.
/// Contains the message being sent and the headers to be attached.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboundContext
{
    /// <summary>
    /// The outbound message (command, notification, or request payload). May be null for 0-parameter service methods.
    /// </summary>
    public object? Message { get; init; }

    /// <summary>
    /// Service provider for resolving dependencies during pipeline execution.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Response result set by the rider or by outbound middleware that short-circuits the pipeline.
    /// Mirrors <see cref="InboundContext.Response"/>.
    /// </summary>
    public IResult? Response { get; set; }
}
