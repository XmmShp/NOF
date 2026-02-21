using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Context for outbound message pipeline execution.
/// Contains the message being sent and the headers to be attached.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboundContext
{
    /// <summary>
    /// The outbound message (command, notification, or request).
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// Headers to be sent with the message.
    /// Outbound middleware populates these (tracing, tenant, JWT, etc.).
    /// Caller-provided headers are merged in before the pipeline runs.
    /// </summary>
    public IDictionary<string, string?> Headers { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional destination endpoint name override.
    /// </summary>
    public string? DestinationEndpointName { get; init; }

    /// <summary>
    /// Response result set by the rider or by outbound middleware that short-circuits the pipeline.
    /// Mirrors <see cref="InboundContext.Response"/>.
    /// </summary>
    public IResult? Response { get; set; }
}
