using NOF.Application;
using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure.Core;

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
    public required IMessage Message { get; init; }

    /// <summary>
    /// Handler instance
    /// </summary>
    public required IMessageHandler Handler { get; init; }

    /// <summary>
    /// Transport-level headers passed from the hosting adapter (HTTP, message bus, etc.).
    /// These are distinct from <see cref="IInvocationContext.Items"/> which is for
    /// cross-cutting application-level state within the invocation scope.
    /// </summary>
    public IDictionary<string, string?> Headers { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Response result (only used for Request handlers)
    /// </summary>
    public IResult? Response { get; set; }

    /// <summary>
    /// Handler type name
    /// </summary>
    public string HandlerType
    {
        get
        {
            return Handler.GetType().FullName ?? Handler.GetType().Name;
        }
    }

    /// <summary>
    /// Message type name
    /// </summary>
    public string MessageType => Message.GetType().FullName ?? Message.GetType().Name;
}

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
