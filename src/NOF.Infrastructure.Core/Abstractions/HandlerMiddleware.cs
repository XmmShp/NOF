using System.ComponentModel;

namespace NOF;

/// <summary>
/// Handler execution context
/// Contains metadata during handler execution
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HandlerContext
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
            var type = Handler.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(StateMachineNotificationHandler<,>))
            {
                var stateMachineType = type.GenericTypeArguments[0];
                return stateMachineType.FullName ?? stateMachineType.Name;
            }

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
public interface IHandlerMiddleware
{
    /// <summary>
    /// Execute middleware logic
    /// </summary>
    /// <param name="context">Handler execution context</param>
    /// <param name="next">Next middleware in the pipeline or the final Handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public static partial class NOFConstants
{
    public const string MessageId = "NOF.Message.MessageId";
    public const string TraceId = "NOF.Message.TraceId";
    public const string SpanId = "NOF.Message.SpanId";
    public const string TenantId = "NOF.TenantId";
}
