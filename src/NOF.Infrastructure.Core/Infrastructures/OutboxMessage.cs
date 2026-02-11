using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Outbox message entity used for adding messages in transactional context and reading by background services.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboxMessage
{
    /// <summary>
    /// The message ID.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The message content (wrapped message).
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// The headers dictionary.
    /// </summary>
    public Dictionary<string, string?> Headers { get; init; } = [];

    /// <summary>
    /// The destination endpoint name.
    /// </summary>
    public string? DestinationEndpointName { get; init; }

    /// <summary>
    /// The creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The retry count (defaults to 0 when added).
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Distributed tracing TraceId (used to restore tracing context).
    /// </summary>
    public ActivityTraceId? TraceId { get; init; }

    /// <summary>
    /// Distributed tracing SpanId (used to restore tracing context).
    /// </summary>
    public ActivitySpanId? SpanId { get; init; }
}
