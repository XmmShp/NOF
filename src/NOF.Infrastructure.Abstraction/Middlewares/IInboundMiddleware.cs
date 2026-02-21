using System.ComponentModel;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Handler execution pipeline delegate
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask InboundDelegate(CancellationToken cancellationToken);

/// <summary>
/// Handler middleware interface
/// Used to insert cross-cutting concerns (such as transactions, logging, validation, etc.) before and after Handler execution
/// </summary>
public interface IInboundMiddleware
{
    /// <summary>
    /// Execute middleware logic
    /// </summary>
    /// <param name="context">Handler execution context</param>
    /// <param name="next">Next middleware in the pipeline or the final Handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken);
}

