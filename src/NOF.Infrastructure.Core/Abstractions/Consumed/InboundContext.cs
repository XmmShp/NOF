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
