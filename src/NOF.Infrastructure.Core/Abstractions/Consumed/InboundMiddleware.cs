using System.ComponentModel;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Handler execution pipeline delegate
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

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
    ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public static partial class NOFInfrastructureCoreConstants
{
    public static partial class Transport
    {
        /// <summary>
        /// Standard HTTP / transport-level header keys used in <see cref="InboundContext.Headers"/>.
        /// </summary>
        public static class Headers
        {
            public const string Authorization = "Authorization";
            public const string TenantId = "NOF.TenantId";
            public const string TraceId = "NOF.Message.TraceId";
            public const string SpanId = "NOF.Message.SpanId";
            public const string MessageId = "NOF.Message.MessageId";
        }
    }
}
