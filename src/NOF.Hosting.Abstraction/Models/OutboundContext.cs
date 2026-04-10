using NOF.Contract;
using System.ComponentModel;

namespace NOF.Hosting;

/// <summary>
/// Outbound message pipeline context.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboundContext
{
    /// <summary>
    /// The outbound message (command, notification, or request payload). Can be null for parameterless methods.
    /// </summary>
    public object? Message { get; init; }

    /// <summary>
    /// Service provider used to resolve dependencies during pipeline execution.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Headers to propagate outbound across processes/protocols. Independent from the ambient execution context.
    /// </summary>
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pipeline response, potentially set by the HTTP client or by a short-circuiting middleware.
    /// </summary>
    public IResult? Response { get; set; }
}
