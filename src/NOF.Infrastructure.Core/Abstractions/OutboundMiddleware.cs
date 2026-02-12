using NOF.Application;
using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure.Core;

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
}

/// <summary>
/// Outbound pipeline delegate.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask OutboundDelegate(CancellationToken cancellationToken);

/// <summary>
/// Outbound middleware interface — mirrors <see cref="IHandlerMiddleware"/> for the outbound direction.
/// Used to insert cross-cutting concerns (JWT propagation, tracing, tenant, etc.) into outbound messages.
/// </summary>
public interface IOutboundMiddleware
{
    /// <summary>
    /// Execute outbound middleware logic.
    /// </summary>
    ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken);
}
