using System.ComponentModel;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Outbound pipeline delegate.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask OutboundDelegate(CancellationToken cancellationToken);

/// <summary>
/// Outbound middleware interface — mirrors <see cref="IInboundMiddleware"/> for the outbound direction.
/// Used to insert cross-cutting concerns (JWT propagation, tracing, tenant, etc.) into outbound messages.
/// </summary>
public interface IOutboundMiddleware
{
    /// <summary>
    /// Execute outbound middleware logic.
    /// </summary>
    ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken);
}
