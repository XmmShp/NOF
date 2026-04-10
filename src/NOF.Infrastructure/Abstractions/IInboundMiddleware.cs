using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Handler execution pipeline delegate.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask InboundDelegate(CancellationToken cancellationToken);

/// <summary>
/// Handler middleware interface.
/// </summary>
public interface IInboundMiddleware
{
    /// <summary>
    /// Executes middleware logic.
    /// </summary>
    ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken);
}
