using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Handler execution context.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InboundContext
{
    /// <summary>
    /// Message instance. May be null for 0-parameter service methods.
    /// </summary>
    public object? Message { get; init; }

    /// <summary>
    /// Service provider for resolving dependencies during pipeline execution.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Response result (only used for request handlers).
    /// </summary>
    public IResult? Response { get; set; }

    /// <summary>
    /// Attributes associated with the message and handler.
    /// </summary>
    public required List<Attribute> Attributes { get; init; }

    /// <summary>
    /// Metadata associated with the message and handler.
    /// </summary>
    public required IDictionary<string, object?> Metadatas { get; init; }
}
