using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

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
    public required object Message { get; init; }

    /// <summary>
    /// Handler type
    /// </summary>
    public required Type HandlerType { get; init; }

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
}
